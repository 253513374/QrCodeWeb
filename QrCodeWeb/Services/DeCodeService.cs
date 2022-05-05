using OpenCvSharp;
using OpenCvSharp.Internal.Vectors;
using QrCodeWeb.Controllers;
using QrCodeWeb.Datas;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;
using ZXing;
using static System.Net.Mime.MediaTypeNames;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace QrCodeWeb.Services
{
    public class DeCodeService
    {
        private const string _wechat_QCODE_detector_prototxt_path = "WechatQrCodeFiles/detect.prototxt";
        private const string _wechat_QCODE_detector_caffe_model_path = "WechatQrCodeFiles/detect.caffemodel";
        private const string _wechat_QCODE_super_resolution_prototxt_path = "WechatQrCodeFiles/sr.prototxt";
        private const string _wechat_QCODE_super_resolution_caffe_model_path = "WechatQrCodeFiles/sr.caffemodel";
        private IWebHostEnvironment Environment { get; }
        private readonly ILogger<DeCodeService> Logger;

        public string FileNmae { get; set; }
        public int FileNmaeIdnex { get; set; } = 1;

        public DeCodeService(IWebHostEnvironment environment, ILogger<DeCodeService> logger)
        {
            Environment = environment;
            Logger = logger;
        }

        string Decode(Mat src,  out Mat dst, string filename = "")
        {
            string _wechat_QCODE_detector_prototxt_path = "WechatQrCodeFiles/detect.prototxt";
            string _wechat_QCODE_detector_caffe_model_path = "WechatQrCodeFiles/detect.caffemodel";
            string _wechat_QCODE_super_resolution_prototxt_path = "WechatQrCodeFiles/sr.prototxt";
            string _wechat_QCODE_super_resolution_caffe_model_path = "WechatQrCodeFiles/sr.caffemodel";

            var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
                                                         _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

            // Mat MedianBlur = new Mat();

            // Cv2.MedianBlur(src, MedianBlur, 11);


            string[] texts;
            Mat[] rects;
            wechatQrcode.DetectAndDecode(src, out rects, out texts);

            
            // wechatQrcode.
            if (texts.Length <= 0)
            {
                dst = null;
                Logger.LogWarning($"二维码解析失败");
                return "";
            }
            Logger.LogInformation($"二维码解析成功，解析内容：{texts[0]}");
            Mat drawingmark = src.Clone();
            List<Point[]> lpoint = new();
            //for (int i = 0; i < rects.Length; i++)
            //{
            var ss = rects[0];
            OutputArray array = ss;
            Point pt1 = new Point((int)ss.At<float>(0, 0), (int)ss.At<float>(0, 1));
            Point pt2 = new Point((int)ss.At<float>(1, 0), (int)ss.At<float>(1, 1));
            Point pt3 = new Point((int)ss.At<float>(2, 0), (int)ss.At<float>(2, 1));
            Point pt4 = new Point((int)ss.At<float>(3, 0), (int)ss.At<float>(3, 1));
            lpoint.Add(new Point[] { pt1, pt2, pt3, pt4 });
            Cv2.DrawContours(drawingmark, lpoint.ToArray(), 0, new Scalar(0, 125, 255), 5, LineTypes.Link8);



            RotatedRect rotatedRect = ss.MinAreaRect();
            var center = rotatedRect.Center;
            var size2F = rotatedRect.Size;
            var MAX = Math.Max(size2F.Width, size2F.Height);
            var RoiSize = new Size(MAX + (MAX / 2), MAX + (MAX / 2));
            var RoiPoint = new Point(center.X - (MAX / 2) - (MAX / 4), center.Y - (MAX / 2) - (MAX / 4));

            Rect rectroi = new Rect(RoiPoint, RoiSize);
            dst = new Mat(src, rectroi);

            //SaveMatFile(dst, "二维码截取", filename);
            //   warpAffine(dst, image, M, sz);
            return texts[0];
        }


        Mat WarpAffine(Mat roi, string filename="")
        {
            Mat src = roi.Clone();
            Mat drawsrc = roi.Clone();
            Mat GRAY_mat = new Mat();
            Cv2.CvtColor(src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图

            //Mat ConvertTo = new Mat();
            //GRAY_mat.ConvertTo(ConvertTo, MatType.CV_8UC1, 2, 7);
            //SaveMatFile(ConvertTo, $"ConvertTo", filename);

            Mat elementErode = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(35, 35));
            Mat Erode = new Mat();
            Cv2.Erode(GRAY_mat, Erode, elementErode);
            // SaveMatFile(Erode, $"Erode", filename);
            
            Mat Threshold_mat = new Mat();
            Cv2.Threshold(Erode, Threshold_mat, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
           // SaveMatFile(Threshold_mat, "WarpAffine_Threshold二值化", filename);

            //Mat Threshold_mat11 = new Mat();
            //Cv2.AdaptiveThreshold(Erode, Threshold_mat11,  255, AdaptiveThresholdTypes.MeanC,ThresholdTypes.BinaryInv,101,2);
            //SaveMatFile(Threshold_mat11, "WarpAffine_AdaptiveThreshold二值化", filename);

            Mat elementDilate1 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            Mat Dilatedst = new Mat();
            Cv2.Dilate(Threshold_mat, Dilatedst, elementDilate1);

           // SaveMatFile(Dilatedst, "WarpAffine_二维码膨胀边界图像", filename);

            Dilatedst.FindContours(out var contours, out var hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length <= 0)
            {
                return null;
            }

            List<Point> pts = new();

            double matarea = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                var area = Cv2.ContourArea(contours[i]);
                if (area > 1000)
                {
                    // matarea = Math.Max(matarea, area);
                    if (area > matarea)
                    {
                        // Cv2.ap
                        matarea = area;
                        pts = contours[i].ToList();
                    }
                }
            }
            if (pts.Count <= 0)
            {
                return null;
            }

            const double whilwecount = 0.05;
            // int count = 0;
            double x = 0.01;
            Point[] tp = new Point[4];
            while (x < whilwecount)
            {
                var rectqq = Cv2.ApproxPolyDP(pts.ToArray(), x * Cv2.ArcLength(pts.ToArray(), true), true);
                x = x + 0.005;
                if (rectqq.Length == 4)
                {
                    tp = rectqq;
                    break;
                }
            }

            if (tp.Length != 4)
            {
                return null;
            }
            using Mat mat = new Mat(src.Size(), MatType.CV_8UC1, new Scalar(0));
            var pp = new List<Point[]>();
            pp.Add(tp.ToArray());
            mat.DrawContours(pp.ToArray(), -1, new Scalar(255), -1);
            SaveMatFile(mat, "WarpAffine_二维码区域掩膜", filename);

            for (int i = 0; i < tp.Length; i++)
            {
                drawsrc.PutText(i.ToString(), tp[i], HersheyFonts.HersheySimplex, 2, new Scalar(255), 2);
                drawsrc.Line(tp[i], tp[(i + 1) % 4], new Scalar(255), 2);
            }
            SaveMatFile(drawsrc, "WarpAffine_圈定二维码区", filename);
            RotatedRect rotated = Cv2.MinAreaRect(tp.ToArray());
            Point2f[] srcpoint = new Point2f[]
            {
                new Point2f(tp[0].X,tp[0].Y),
                new Point2f(tp[1].X,tp[1].Y),
                new Point2f(tp[2].X,tp[2].Y),
                new Point2f(tp[3].X,tp[3].Y)
            };
            //定义变换之后的二维码Size
            int sizew = Math.Max((int)rotated.Size.Width + 50, (int)rotated.Size.Height + 50);
            int boxw = 30;
            Rect rect = new Rect(0, 0, sizew, sizew);
            Point2f[] dstpoint = new Point2f[4];
            dstpoint[0] = new Point2f(boxw, boxw);
            dstpoint[1] = new Point2f(boxw, sizew - boxw);
            dstpoint[2] = new Point2f(sizew - boxw, sizew - boxw);
            dstpoint[3] = new Point2f(sizew - boxw, boxw);

            //对二值化的二维码区域进行透视变换，
            using Mat warpMatrix = Cv2.GetPerspectiveTransform(srcpoint, dstpoint);
            Mat dst = new Mat(rect.Size, MatType.CV_8UC3);
            Cv2.WarpPerspective(roi, dst, warpMatrix, dst.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

            // dst.ConvertTo(dst, MatType.CV_8UC1, 2, 10);
            SaveMatFile(dst, "透视变换结果", filename);


            return dst;
        }

        public Task DetectAndDecode(Mat image, ref ResponseModel response)
        {
            Mat SrcRoi = new Mat();
            var code = Decode(image, out SrcRoi);
            SaveMatFile(SrcRoi, "截取二维码区域");            
            response.DeQRcodeContent = code;
            if (code.Length == 0)
            {
                response.Code = "501";
                response.Message = "找不到二维码";
                return Task.CompletedTask;
            }

            ///获取完整二维码
            Mat warpaffinemat =  WarpAffine(SrcRoi.Clone());
           // SaveMatFile(warpaffinemat, "warpaffinemat");            
            ///图形预处理，方便查找定位点
            Mat mat_morphologyEx = MatPreprocessing(warpaffinemat.Clone());
            //查找二维码的三个定位点
            List<RectPoints> PatternsPoints = GetPosotionDetectionPatternsPoints(mat_morphologyEx);

            if (PatternsPoints.Count != 3)
            {
                response.Code = "502";
                response.Message = "没有找到二维码定位点";
                Log.Information($"没有找到二维码定位点");
                return Task.CompletedTask;
            }
            //二维码方向矫正
            Point2f center;
            int rotationAngle = GetRotationAngle(PatternsPoints, out center);
            Mat m = Cv2.GetRotationMatrix2D(center, rotationAngle, 1);
            Mat dst = new Mat();
            Cv2.WarpAffine(warpaffinemat, dst, m, dst.Size());
            SaveMatFile(dst, $"矫正之后的原图");

            //获取指定的识别定位点
            Log.Information($"成功找到二维码3个定位点：坐标:{PatternsPoints[0].CenterPoints.X},{PatternsPoints[0].CenterPoints.Y}--{PatternsPoints[1].CenterPoints.X},{PatternsPoints[1].CenterPoints.Y}--{PatternsPoints[2].CenterPoints.X},{PatternsPoints[2].CenterPoints.Y}");
            using Mat Patterns = GetPosotionDetectionPatternsMat(dst, PatternsPoints);
             
            //图像转Base64编码    
            response.MarkImgData = Base64ToMat.ToBase64(Patterns);
            response.Code = "200";
            response.Message = "成功找到二维码锯齿定位点";

            Log.Information($"返回二维码右上角定位点图片base64");
            SaveMatFile(Patterns, "Patterns");
            
            //如果没有找到方块，则返回
            return Task.CompletedTask;
        }

        /// <summary>
        /// 计算向量 两点之间的距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private double Distance(Point2f a, Point2f b)
        {
            var selfx = Math.Abs(a.X - b.X);
            var selfy = Math.Abs(a.Y - b.Y);
            var selflen = Math.Sqrt((selfx * selfx) + (selfy * selfy));
            return selflen;
        }


        /// <summary>
        /// 图像预处理
        /// </summary>
        /// <param name="srcmat">需要处理的图像</param>
        /// <returns></returns>
        private Mat MatPreprocessing(Mat src, string filename="")
        {

            Mat GRAY_mat = new Mat();
            Cv2.CvtColor(src.Clone(), GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图

            //进行内核大小为7的 中值滤波
            using Mat MedianBlur = new Mat();
            Cv2.MedianBlur(GRAY_mat, MedianBlur, 1);
            SaveMatFile(MedianBlur, "MedianBlur", filename);

            ///使用自适应阈值来二值化图像矩阵数据
            Mat srthreshold = new Mat();
            //  Cv2.Threshold(MedianBlur, Threshold_mat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            // SaveMatFile(Threshold_mat, $"Threshold_Binary", filename);
            Cv2.AdaptiveThreshold(MedianBlur, srthreshold, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 101, 5);

            SaveMatFile(srthreshold, "自适应阈值二值化", filename);

            //Mat DST = new Mat();
            //Cv2.Normalize(MedianBlur, DST, 0, 255, NormTypes.MinMax, 1);
            // SaveMatFile(Threshold_mat, $"AdaptiveThreshold", filename);

            //闭运算
            //Mat MorphologyEx_Close = new Mat();
            //Mat elementClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            //Cv2.MorphologyEx(GRAY_mat, MorphologyEx_Close, MorphTypes.Close, elementClose);
            //SaveMatFile(MorphologyEx_Close, "MorphologyEx_Close", filename);


            int moduleSzie = (int)((GRAY_mat.Width / 31) / 2);

            //膨胀运算，使用7*7的矩形核
            Mat MorphologyEx_Dilate = new Mat();
            Mat elementDilate = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
            Cv2.MorphologyEx(srthreshold, MorphologyEx_Dilate, MorphTypes.Dilate, elementDilate);
            SaveMatFile(MorphologyEx_Dilate, "MorphologyEx_Open", filename);

            //腐蚀运算，使用3*3的矩形核
            Mat MorphologyEx_Erode = new Mat();
            Mat elementErode = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(MorphologyEx_Dilate, MorphologyEx_Erode, MorphTypes.Erode, elementErode);
            SaveMatFile(MorphologyEx_Erode, "MorphologyEx_Erode", filename);

            //Mat Gradient = new Mat();
            //Mat elementGradient = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(5, 5));
            //Cv2.MorphologyEx(MorphologyEx_Close, Gradient, MorphTypes.Gradient, elementClose);
            //SaveMatFile(Gradient, "elementGradient", filename);
         
            return MorphologyEx_Erode;

        }



        List<RectPoints> GetPosotionDetectionPatternsPoints(Mat dilatemat, string name="")
        {
            try
            {
                List<RectPoints> rectPoints = new List<RectPoints>();
                Point[][] matcontours;
                HierarchyIndex[] hierarchy;
                ///算出二维码轮廓
                Cv2.FindContours(dilatemat, out matcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                using Mat drawingmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingmarkminAreaSzie = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingAllmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);

                using Mat drawing = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                using Mat drawingf = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
                // 轮廓圈套层数
                rectPoints = new List<RectPoints>();

                double moduleSzie = dilatemat.Width / 31;
                double minAreaSzie = (moduleSzie * 4) * (moduleSzie * 4);
                double maxAreaSzie = (moduleSzie * 12) * (moduleSzie * 12);
                //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
                int ic = 0;
                int parentIdx = -1;
                for (int i = 0; i < matcontours.Length; i++)
                {
                    //int ic = 0;
                    //int childIdx = i;
                    //while (hierarchy[childIdx].Child != -1)
                    //{
                    //    childIdx = hierarchy[childIdx].Child;
                    //    ic = ic + 1;
                    //}
                    //if (hierarchy[childIdx].Child != -1)
                    //{
                    //    ic = ic + 1;
                    //}
                    var area2 = (int)Cv2.ContourArea(matcontours[i], false);
                    if (area2 <= minAreaSzie || area2 >= maxAreaSzie) { continue; }

                    var approxPolyDP = Cv2.ApproxPolyDP(matcontours[i], 0.03 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);// 

                    if (approxPolyDP.Length != 4)
                    {
                        //Cv2.DrawContours(drawing, matcontours, i, new Scalar(0, 125, 255), 4, LineTypes.Link8);
                        //SaveMatFile(drawing, "无法逼近的轮廓", name);
                        continue;
                    }

                    //if (approxPolyDP.Length == 4) {
                    //    Cv2.DrawContours(drawingf, matcontours, i, new Scalar(255, 255, 255));
                    //    SaveMatFile(drawingf, "逼近4的轮廓", name);
                    //}
                    Cv2.DrawContours(drawingAllContours, matcontours, i, new Scalar(255, 255, 255));
                    SaveMatFile(drawingAllContours, "所有轮廓", name);
                    #region

                    if (hierarchy[i].Child != -1 && ic == 0)
                    {
                        parentIdx = i;
                        ic++;
                    }
                    else if (hierarchy[i].Child != -1)
                    {
                        ic++;
                    }
                    else if (hierarchy[i].Child == -1)
                    {
                        ic = 0;
                        parentIdx = -1;
                    }
                    #endregion
                    //有两个子轮廓
                    if (ic >= 2)
                    {
                        //画出所有轮廓图
                        // 保存找到的三个黑色定位角
                        var points2 = Cv2.ApproxPolyDP(matcontours[i], 0.03 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);// 
                        if (points2.Length == 4)
                        {
                            var area = (int)Cv2.ContourArea(matcontours[i], false);
                            if (area > minAreaSzie && area < maxAreaSzie)
                            {
                                RotatedRect rotated = Cv2.MinAreaRect(points2);
                                var w = rotated.Size.Width;
                                var h = rotated.Size.Height;
                                if (Math.Min(w, h) / Math.Max(w, h) > 0.7)
                                {
                                    var rects = new RectPoints()
                                    {
                                        CenterPoints = rotated.Center.ToPoint(),
                                        MarkPoints = points2,
                                        Angle = rotated.Angle
                                    };
                                    //rectPoints.FindLast(x => x.CenterPoints == rects.CenterPoints).MarkPoints = points2;
                                    rectPoints.Add(rects);
                                    //画出三个黑色定位角的轮廓
                                    Cv2.DrawContours(drawingmark, matcontours, i, new Scalar(0, 125, 255), 1, LineTypes.Link8);

                                }
                            }
                            else
                            {
                                Cv2.DrawContours(drawingmarkminAreaSzie, matcontours, i, new Scalar(0, 125, 255), 4, LineTypes.Link8);

                            }
                        }
                        //else
                        //{
                        //    Cv2.DrawContours(drawing, matcontours, i, new Scalar(0, 125, 255), 4, LineTypes.Link8);
                        //    SaveMatFile(drawing, "无法逼近的轮廓", name);
                        //}
                        ic = 0;
                        parentIdx = -1;

                    }

                }
                SaveMatFile(drawingmark, "三个定位点轮廓", name);
                SaveMatFile(drawingAllContours, "所有轮廓", name);
                SaveMatFile(drawingmarkminAreaSzie, "三个定位点轮廓_小于指定面积", name);



                return rectPoints;
            }
            catch (Exception ee)
            {
                return new List<RectPoints>();
                // throw;
            }

        }

        Mat GetPosotionDetectionPatternsMat(Mat mat, List<RectPoints> rectPoints)
        {
            var maxw = rectPoints.Max(w => w.CenterPoints.X);
            var minh = rectPoints.Min(w => w.CenterPoints.Y);
            var prect = rectPoints.FindLast(f => f.CenterPoints.X == maxw);
            ///计算其中一个定位点周长，再根据周长计算单个二维码模块像素大小
            var ArcLength = Cv2.ArcLength(prect.MarkPoints, true);
            //单边长
            var le = (int)(ArcLength / 4);
            //二维码模块像素Size,取值5，是因为轮廓取的"回"中间的轮廓，按照比例是占的5个模块大小
            var ModuleSize = (int)(le / 5) + 2;
            
            var topx = (int)(maxw - ModuleSize * 5);
            var topy = (int)(minh - ModuleSize * 5);
            topy = topy < 0 ? 1 : topy;//防止越界

            var width = mat.Width - topx - 1;

            var rectwidth = ModuleSize * 11;
            var w = (ModuleSize * 11) > width ? width : (ModuleSize * 10);

            Rect rect = new Rect(topx, topy, w, w);//取得11个模块宽度
            Mat roi = new Mat(mat, rect);
            Log.Information($"根据二维码模块大小，截取右上角定位点11个模块宽度：宽{roi.Width},高{roi.Height}");
            return roi;
        }


        int GetRotationAngle(List<RectPoints> rectPoints, out Point2f QRMatCenterPoint)
        {

            //Log.Information($"计算二维码边界坐标顺序：开始");
            //var width = rectPoints.Max(x => x.MarkPoints[0].X);
            int a = 0, b = 1, c = 2, d = 3;
            //计算三个定位点的距离
            int AB = (int)Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
            int BC = (int)Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
            int AC = (int)Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);
            //计算二维码的中心坐标,计算二维码定位点之间最长向量，二维码的中点为 最长向量的中心坐标。
            var max = Math.Max(AB, Math.Max(BC, AC));
            Point topPoint = new Point();
            QRMatCenterPoint = new Point();
            int selecttop = 0;

            if (max == AB)
            {
                topPoint = rectPoints[c].CenterPoints;//二维码直角顶点
                selecttop = 2;
                //计算二维码的中心坐标
                QRMatCenterPoint.X = (rectPoints[a].CenterPoints.X + rectPoints[b].CenterPoints.X) / 2;
                QRMatCenterPoint.Y = (rectPoints[a].CenterPoints.Y + rectPoints[b].CenterPoints.Y) / 2;
            }
            else if (max == BC)
            {
                selecttop = 0;
                topPoint = rectPoints[a].CenterPoints;
                QRMatCenterPoint.X = (rectPoints[b].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
                QRMatCenterPoint.Y = (rectPoints[b].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;
            }
            else if (max == AC)
            {
                selecttop = 1;
                topPoint = rectPoints[b].CenterPoints;
                QRMatCenterPoint.X = (rectPoints[a].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
                QRMatCenterPoint.Y = (rectPoints[a].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;
            }

            int RotationAngle = -1;
            ///计算角度,校验点
            //Point DefaultTopPoint = new Point(QRMatCenterPoint.X + 300, QRMatCenterPoint.Y - 300);
            //int Sdirection = (DefaultTopPoint.X - topPoint.X) * (DefaultTopPoint.Y - topPoint.Y) - (DefaultTopPoint.Y - topPoint.Y) * (DefaultTopPoint.X - topPoint.Y);
            //if (Sdirection == 0)
            //{
            //    if (topPoint.X < QRMatCenterPoint.X)
            //    {
            //        RotationAngle = 0;
            //    }
            //    else
            //    {
            //        RotationAngle = 180;
            //    }
            //}
            //else
            //{
            //    int aa = (int)Distance(DefaultTopPoint, QRMatCenterPoint);
            //    int bb = (int)Distance(topPoint, QRMatCenterPoint);
            //    int cc = (int)Distance(QRMatCenterPoint, DefaultTopPoint);
            //    RotationAngle = (int)((180 / Math.PI) * (Math.Acos((aa * aa - bb * bb - cc * cc) / (-2 * bb * cc))));//#旋转角
            //    if (Sdirection < 0) RotationAngle = 360 - RotationAngle;
            //}

            Point[] QRrect = new Point[4];

            //计算二维码矩形的第四个点
            Point markPosotion = new Point();
            if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                #region 第一象限

                //
                //var topx = rectPoints[selecttop].MarkPoints.Min(s => s.X);
                //var topy = rectPoints[selecttop].MarkPoints.Min(s => s.Y);
                //QRrect[a] = new Point(topx, topy);
                //for (int i = 0; i < rectPoints.Count; i++)
                //{
                //    if (i != selecttop)
                //    {
                //        var marks = rectPoints[i].MarkPoints;
                //        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                //        {
                //            var bx = marks.Max(m => m.X);
                //            var by = marks.Min(m => m.Y);
                //            QRrect[b] = new Point(bx, by);
                //        }
                //        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                //        {
                //            var dx = marks.Min(m => m.X);
                //            var dy = marks.Max(m => m.Y);
                //            QRrect[d] = new Point(dx, dy);
                //        }
                //    }
                //}
                //var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                //var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                //QRrect[c] = new Point(markPosotionx, markPosotiony);
                #endregion
                RotationAngle = 0;
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {

                #region 第二象限
                //var topx = rectPoints[selecttop].MarkPoints.Max(s => s.X);
                //var topy = rectPoints[selecttop].MarkPoints.Min(s => s.Y);
                //QRrect[a] = new Point(topx, topy);

                //for (int i = 0; i < rectPoints.Count; i++)
                //{
                //    if (i != selecttop)
                //    {
                //        var marks = rectPoints[i].MarkPoints;
                //        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                //        {
                //            var cx = marks.Max(m => m.X);
                //            var cy = marks.Max(m => m.Y);
                //            QRrect[b] = new Point(cx, cy);
                //        }
                //        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                //        {
                //            var dx = marks.Min(m => m.X);
                //            var dy = marks.Min(m => m.Y);
                //            QRrect[d] = new Point(dx, dy);
                //        }
                //    }
                //}
                //var markPosotionx = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - QRrect[b].X);
                //var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[b].Y);
                //QRrect[c] = new Point(markPosotionx, markPosotiony);
                #endregion

                RotationAngle = 90;

            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {

                #region 第三象限
                //var ax = rectPoints[selecttop].MarkPoints.Max(s => s.X);
                //var ay = rectPoints[selecttop].MarkPoints.Max(s => s.Y);
                //QRrect[a] = new Point(ax, ay);

                //for (int i = 0; i < rectPoints.Count; i++)
                //{
                //    if (i != selecttop)
                //    {
                //        var marks = rectPoints[i].MarkPoints;
                //        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                //        {
                //            var bx = marks.Min(m => m.X);
                //            var by = marks.Max(m => m.Y);
                //            QRrect[b] = new Point(bx, by);
                //        }
                //        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                //        {
                //            var dx = marks.Max(m => m.X);
                //            var dy = marks.Min(m => m.Y);
                //            QRrect[d] = new Point(dx, dy);
                //        }
                //    }
                //}
                //var markPosotionx = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                //var markPosotiony = QRMatCenterPoint.Y - Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                //QRrect[c] = new Point(markPosotionx, markPosotiony);
                #endregion

                RotationAngle = 180;

            }
            else if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {

                #region 第四象限
                //var topx = rectPoints[selecttop].MarkPoints.Min(s => s.X);
                //var topy = rectPoints[selecttop].MarkPoints.Max(s => s.Y);
                //QRrect[a] = new Point(topx, topy); 

                //for (int i = 0; i < rectPoints.Count; i++)
                //{
                //    if (i != selecttop)
                //    {
                //        var marks = rectPoints[i].MarkPoints;
                //        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                //        {
                //            var dx = marks.Max(m => m.X);
                //            var dy = marks.Max(m => m.Y);
                //            QRrect[d] = new Point(dx, dy); 
                //        }
                //        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                //        {
                //            var bx = marks.Min(m => m.X);
                //            var by = marks.Min(m => m.Y);
                //            QRrect[b] = new Point(bx, by);
                //        }
                //    }
                //}
                //var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                //var markPosotiony = QRMatCenterPoint.Y - Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                //QRrect[c] = new Point(markPosotionx, markPosotiony);
                #endregion
                RotationAngle = -90;
            }

            //wmax = Math.Max((int)Distance(QRrect[a], QRrect[c]), (int)Distance(QRrect[c], QRrect[d]));
            //Mat M = Cv2.GetRotationMatrix2D(QRMatCenterPoint, rectPoints[0].Angle, 1.0);
            //Mat dst = new Mat();
            //Cv2.WarpAffine(mat, dst, M, mat.Size(), InterpolationFlags.Cubic, BorderTypes.Replicate);
            //SaveMatFile(dst, "WarpAffine");
            //Log.Information($"计算二维码边界坐标顺序：完成，坐标顺序：{QRrect[0].X},{QRrect[0].Y};{QRrect[1].X},{QRrect[1].Y};{QRrect[2].X},{QRrect[2].Y};{QRrect[3].X},{QRrect[3].Y}");
            return RotationAngle;
        }


        private Task SaveMatFile(Mat array, string name,string s="")
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"testdata");
            if (!Directory.Exists(filepathw))
            {
                Directory.CreateDirectory(filepathw);
            }

            var filepathw2 = Path.Combine(filepathw, $"{FileNmae}");

            if (!Directory.Exists(filepathw2))
            {
                Directory.CreateDirectory(filepathw2);
            }

            var filepath = Path.Combine(filepathw2, $"{FileNmaeIdnex}_{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);

            FileNmaeIdnex++;
            return Task.CompletedTask;
        }
    }
}