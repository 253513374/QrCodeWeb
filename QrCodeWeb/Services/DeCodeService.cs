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

        public string Decode(Mat src,out Mat dst)
        {
            var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
                                                         _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

            string[] texts;
            Mat[] rects;
            wechatQrcode.DetectAndDecode(src, out rects, out texts);

            // wechatQrcode.
            if (texts.Length <= 0) {
                dst = null;
                return "";
            }
          
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

            SaveMatFile(drawingmark, "二维码边界");
                
            RotatedRect rotatedRect = ss.MinAreaRect();
            var center = rotatedRect.Center;
            var size2F = rotatedRect.Size;
            var MAX =  Math.Max(size2F.Width, size2F.Height);
            var RoiSize = new Size(MAX+(MAX/2), MAX + (MAX / 2));
            var RoiPoint = new Point(center.X - (MAX/2)-(MAX/4), center.Y - (MAX / 2) - (MAX / 4));

            Rect rectroi = new Rect(RoiPoint, RoiSize);
            dst = new Mat(src, rectroi);
            //   warpAffine(dst, image, M, sz);
            return texts[0];
        }


        private Mat WarpAffine( Mat roi)
        {

            Mat src = roi.Clone();


            Mat GRAY_mat = new Mat();
            Cv2.CvtColor(src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图            
            Mat Threshold_mat = new Mat();
            Cv2.Threshold(GRAY_mat, Threshold_mat, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            Mat elementDilate1 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(25,25));
            Mat Dilatedst = new Mat();
            Cv2.Dilate(Threshold_mat, Dilatedst, elementDilate1);

            SaveMatFile(Dilatedst, "二维码膨胀边界图像");
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
            if (pts.Count <= 0) return null;

             //= Cv2.ApproxPolyDP(pts.ToArray(), 0.035 * Cv2.ArcLength(pts.ToArray(), true), true);


            const int whilwecount = 8;
            int count = 0;
            double x = 0.01;
            Point[] tp = new Point[4];
            while (count< whilwecount)
            {
                var rectqq = Cv2.ApproxPolyDP(pts.ToArray(), x * Cv2.ArcLength(pts.ToArray(), true), true);
                x = x + 0.05;
                if (rectqq.Length == 4)
                {
                    tp = rectqq;
                    break;
                }
            }

            using Mat mat = new Mat(src.Size(), MatType.CV_8UC1, new Scalar(0));
            var pp= new List<Point[]>();
            pp.Add(tp.ToArray());
            mat.DrawContours(pp.ToArray(), -1, new Scalar(255), -1);
            SaveMatFile(mat, "二维码膨胀边界图像轮廓");

            for (int i = 0; i < tp.Length; i++)
            {
                //  Console.WriteLine(tp[i].X + "," + tp[i].Y);

                src.PutText(i.ToString(),tp[i],HersheyFonts.HersheySimplex,2, new Scalar(255), 2);                
                src.Line(tp[i], tp[(i + 1) % 4], new Scalar(255), 2);
            }

            SaveMatFile(src, "二维码位置图像轮廓");

            var rect = Cv2.MinAreaRect(pts.ToArray());
            var center = rect.Center;
            var angle = rect.Angle;
            var M = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            Mat dst = new Mat();
            Cv2.WarpAffine(roi, dst, M, roi.Size());
            return dst;
        }

        public string DetectAndDecode(Mat image, ref ResponseModel response)
        {
           // Mat Preprocessing_mat = MatPreprocessing(image.Clone(), ref response,true);
          //  SaveMatFile(Preprocessing_mat, "Preprocessing_mat");

            Mat SrcRoi = new Mat();
            var code = Decode(image, out SrcRoi);
            SaveMatFile(SrcRoi, "截取二维码区域");            
            response.DeQRcodeContent = code;
            if (code != null && code.Length == 0)
            {
                response.Message = "找不到二维码";
                return null;
            }

            Mat warpaffinemat =  WarpAffine(SrcRoi.Clone());

            SaveMatFile(warpaffinemat, "warpaffinemat");            

            Mat Preprocessing_mat = MatPreprocessing(warpaffinemat.Clone(), ref response, true);
            //SaveMatFile(Roi_MatPreprocessing, "Roi_MatPreprocessing");
            //二维码的三个定位点
            List<RectPoints> rectPoints = new List<RectPoints>();
            GetPosotionDetectionPatternsPoints(Preprocessing_mat.Clone(), out rectPoints);
            if (rectPoints.Count != 3)
            {
                response.Code = "503";
                response.Message = "没有找到二维码";
                Log.Information($"没有找到二维码");
                return "";
            }
            if (rectPoints.Count == 3)
            {
                //根据定位点获取完整二维码
                int moduleSize = 0;
                using Mat qrCodeAreaRectMat = GetQrCodeMat(Preprocessing_mat, rectPoints, out moduleSize);

                SaveMatFile(qrCodeAreaRectMat, "qrCodeAreaRectMat");
                //对完整二维码重新处理
                using Mat qrCodeAreaRectMatpre = MatPreprocessing(qrCodeAreaRectMat.Clone(), ref response);
                //二维码的三个定位点
                List<RectPoints> PatternsPoints = new List<RectPoints>();
                GetPosotionDetectionPatternsPoints(qrCodeAreaRectMatpre, out PatternsPoints);
                if (PatternsPoints.Count < 3)
                {
                    response.Code = "504";
                    response.Message = "无法识别二维码";
                    Log.Information($"截取到的完整二维码无法识别");                    
                    return "";
                }
               
                Log.Information($"成功找到二维码定位点：坐标:{PatternsPoints[0].CenterPoints.X},{PatternsPoints[0].CenterPoints.Y}--{PatternsPoints[1].CenterPoints.X},{PatternsPoints[1].CenterPoints.Y}--{PatternsPoints[2].CenterPoints.X},{PatternsPoints[2].CenterPoints.Y}");
                using Mat Patterns = GetPosotionDetectionPatternsMat(qrCodeAreaRectMat, PatternsPoints, moduleSize);
             
                
                response.MarkImgData = Base64ToMat.ToBase64(Patterns);

                Log.Information($"二维码右上角定位点图片base64");
                response.Code = "200";
                response.Message = "成功找到二维码锯齿定位点";
                SaveMatFile(Patterns, "Patterns");
            }
            // 如果没有找到方块，则返回
            return "数据解码成功";
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
        /// 截取完整二维码
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="rectPoints"></param>
        /// <param name="moduleSize"></param>
        /// <returns></returns>
        private Mat GetQrCodeMat(Mat mat, List<RectPoints> rectPoints, out int moduleSize)
        {
            int ModelLength = 0;
            //根据3个二维码定位点计算二维码模块的平均大小
            for (int i = 0; i < rectPoints.Count; i++)
            {
                RectPoints rectPoint = rectPoints[i];
                ModelLength = ModelLength + (int)Cv2.ArcLength(rectPoint.MarkPoints, true);
            }
            moduleSize = (int)((ModelLength / 3) / 28);
            Logger.LogInformation($"计算二维码模块大小：{moduleSize}");
            var width = 0; //Preprocessing_mat.Width;
            //根据轮廓坐标 计算二维码位置
            Point[] src = GetQrCodePoints(rectPoints, out width, mat);

            Point2f[] src_coners = new Point2f[]
                {
                new Point2f(src[0].X,src[0].Y),
                    new Point2f(src[1].X,src[1].Y),
                    new Point2f(src[2].X,src[2].Y),
                    new Point2f(src[3].X,src[3].Y)
                };
            Rect rect = new Rect(moduleSize * 3, moduleSize * 3, width + moduleSize * 3, width + moduleSize * 3);

            Point2f[] dst_coners = new Point2f[]
            {
                new Point2f(rect.X,rect.Y),
                new Point2f(width,rect.Y),
                new Point2f(width,width),
                new Point2f(rect.X,width),
            };

            using Mat warpMatrix = Cv2.GetPerspectiveTransform(src_coners, dst_coners);
            Mat dst = new Mat(rect.Size, MatType.CV_8UC3);
            Cv2.WarpPerspective(mat, dst, warpMatrix, dst.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

            Logger.LogInformation($"二维码透视变换完成：宽{width}，高{width}");
            //Rect rect = rotated.BoundingRect();
            //根据位置截图二维码区域
            SaveMatFile(dst, "WarpPerspective");

            return dst;
        }

        /// <summary>
        /// 图像预处理
        /// </summary>
        /// <param name="srcmat">需要处理的图像</param>
        /// <returns></returns>
        private Mat MatPreprocessing(Mat src, ref ResponseModel response, bool isdecode = false)
        {
            using Mat GRAY_mat = new Mat();
            SaveMatFile(src, $"原图");
            Cv2.CvtColor(src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图

            using Mat ScaleAbs_mat = new Mat();
            // Cv2.ConvertScaleAbs(GRAY_mat, ScaleAbs_mat, 2, 7);//
            GRAY_mat.ConvertTo(ScaleAbs_mat, MatType.CV_8UC1, 2, 10);
            SaveMatFile(ScaleAbs_mat, $"ScaleAbs_mat257");
            InputArray kernel2 = InputArray.Create<int>(new int[3, 3] { { 0, -1, 0 }, { -1, 5, -1 }, { 0, -1, 0 } });
            using Mat filter2Dmat = new Mat();
            Cv2.Filter2D(ScaleAbs_mat, filter2Dmat, MatType.CV_8UC1, kernel2, anchor: new Point(1, 1), delta: -100, borderType: BorderTypes.Constant);

            SaveMatFile(filter2Dmat, $"filter2Dmat");
            using Mat MedianBlur = new Mat();
            Cv2.MedianBlur(filter2Dmat, MedianBlur, 11);
            SaveMatFile(MedianBlur, $"MedianBlur");


            Mat Threshold_mat = new Mat();
            // Cv2.Threshold(MedianBlur, Threshold_mat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.AdaptiveThreshold(MedianBlur, Threshold_mat, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 57, 2);
            SaveMatFile(Threshold_mat, $"Threshold_Binary");


            Mat elementDilate1 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Mat Dilatedst = new Mat();
            Cv2.Dilate(Threshold_mat, Dilatedst, elementDilate1);
            SaveMatFile(Dilatedst, $"Dilatedst");


            Mat Erode_filter2Dmat = new Mat();
            Mat elementClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(Dilatedst, Erode_filter2Dmat, MorphTypes.Close, elementClose);
            SaveMatFile(Erode_filter2Dmat, "MorphologyEx_Dilate");
            return Erode_filter2Dmat;
        }

        private Mat SetMorphologyEx(Mat mat, ref ResponseModel response, bool isdecode = false)
        {

            using Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

            Mat MorphologyEx_mat = new Mat();
            Cv2.MorphologyEx(mat, MorphologyEx_mat, MorphTypes.Gradient, element);
            SaveMatFile(MorphologyEx_mat, "MorphologyEx_Gradient");
            return MorphologyEx_mat;
        }

        /// <summary>
        /// 返回二维码定位点 <param name="dilatemat">需要解析的原图像MAT</param><param
        /// name="CenterPoints">二维码定位点的中心坐标，一个轮廓一个中心坐标</param><param
        /// name="PatternsPoints">二维码定位点的轮廓坐标集合 是一个二维码数组，返回的集合为所有轮廓的坐标</param>
        /// </summary>
        /// <param name="dilatemat">需要解析的原图像MAT</param>
        /// <param name="CenterPoints">二维码定位点的中心坐标，一个轮廓一个中心坐标</param>
        /// <param name="PatternsPoints">二维码定位点的轮廓坐标集合 是一个二维码数组，返回的集合为所有轮廓的坐标</param>
        /// <returns></returns>
        private Task GetPosotionDetectionPatternsPoints(Mat dilatemat, out List<RectPoints> rectPoints)
        {
            Point[][] matcontours;
            HierarchyIndex[] hierarchy;
            ///算出二维码轮廓
            Cv2.FindContours(dilatemat, out matcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            using Mat drawingmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            //using Mat drawingAllmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            using Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            // 轮廓圈套层数
            rectPoints = new List<RectPoints>();
            //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
            int ic = 0;
            int parentIdx = -1;
            float AngleCount = 0;
            float angle = 0;
            for (int i = 0; i < matcontours.Length; i++)
            {
                //画出所有轮廓图
                Cv2.DrawContours(drawingAllContours, matcontours, parentIdx, new Scalar(255, 255, 255));
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
                //有两个子轮廓
                if (ic ==2)
                {
                    //保存找到的三个黑色定位角
                    var points2 = Cv2.ApproxPolyDP(matcontours[parentIdx], 25, true);
                    if (points2.Length == 4)
                    {
                        //  int ArcLength = (int)Cv2.ArcLength(matcontours[parentIdx], true);
                        RotatedRect rotated = Cv2.MinAreaRect(points2);
                        var w = rotated.Size.Width;
                        var h = rotated.Size.Height;
                        if (Math.Min(w, h) / Math.Max(w, h) > 0.7)
                        {
                            AngleCount = AngleCount + 1.0f;
                           // angle = angle+
                            var rects = new RectPoints()
                            {
                                CenterPoints = rotated.Center.ToPoint(),
                                MarkPoints = points2,
                                Angle =rotated.Angle
                        };
                            rectPoints.Add(rects);
                            //画出三个黑色定位角的轮廓
                            Cv2.DrawContours(drawingmark, matcontours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                        }
                    }
                }
            }
            SaveMatFile(drawingmark, "drawingmark");
            SaveMatFile(drawingAllContours, "drawingAllContours");

            return Task.CompletedTask;
        }

        private Mat GetPosotionDetectionPatternsMat(Mat mat, List<RectPoints> rectPoints, int moduleSize)
        {
            var maxw = rectPoints.Max(w => w.CenterPoints.X);
            // var maxH = rectPoints.Max(w => w.CenterPoints.Y);

            var prect = rectPoints.FindLast(f => f.CenterPoints.X == maxw);

            var minx = prect.MarkPoints.Min(m => m.X);
            var maxy = prect.MarkPoints.Min(m => m.Y);

            var ArcLength = Cv2.ArcLength(prect.MarkPoints, true);

            var le = (int)(ArcLength / 4);//单边长
            var px = (int)(le / 7);//二维码模块大小

            var x = (int)(minx - px * 2);
            var y = (int)(maxy - px * 2) > 0 ? (int)(maxy - px * 2) : (int)(maxy - px * 1);

            var width = mat.Width - x - 1;

            var w = (px * 11) > width ? width : (px * 11);

            Rect rect = new Rect(x, y, w, w);//取得11个模块宽度
            Mat roi = new Mat(mat, rect);

            Log.Information($"根据二维码模块大小，截取右上角定位点11个模块宽度：宽{roi.Width},高{roi.Height}");
            return roi;
        }

        /// <summary>
        /// 返回二维码区域Rect
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private Point[] GetQrCodePoints(List<RectPoints> rectPoints, out int wmax, Mat mat)
        {

            Log.Information($"计算二维码边界坐标顺序：开始");            
            //var width = rectPoints.Max(x => x.MarkPoints[0].X);
            int a = 0, b = 1, c = 2, d = 3;
            //计算三个定位点的距离
            int AB = (int)Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
            int BC = (int)Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
            int AC = (int)Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);
            //计算二维码的中心坐标,计算二维码定位点之间最长向量，二维码的中点为 最长向量的中心坐标。
            var max = Math.Max(AB, Math.Max(BC, AC));
            Point topPoint = new Point();
            Point QRMatCenterPoint = new Point();
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
            Point DefaultTopPoint = new Point(QRMatCenterPoint.X + 300, QRMatCenterPoint.Y - 300);

            int Sdirection = (DefaultTopPoint.X - topPoint.X) * (DefaultTopPoint.Y - topPoint.Y) - (DefaultTopPoint.Y - topPoint.Y) * (DefaultTopPoint.X - topPoint.Y);

            if (Sdirection == 0)
            {
                if (topPoint.X < QRMatCenterPoint.X)
                {
                    RotationAngle = 0;
                }
                else
                {
                    RotationAngle = 180;
                }
            }
            else
            {
                int aa = (int)Distance(DefaultTopPoint, QRMatCenterPoint);
                int bb = (int)Distance(topPoint, QRMatCenterPoint);
                int cc = (int)Distance(QRMatCenterPoint, DefaultTopPoint);
                RotationAngle = (int)((180 / Math.PI) * (Math.Acos((aa * aa - bb * bb - cc * cc) / (-2 * bb * cc))));//#旋转角

                if (Sdirection < 0) RotationAngle = 360 - RotationAngle;
                //if (Sdirection > 0)
                //{
                //    RotationAngle = (int)Math.Atan((DefaultTopPoint.Y - topPoint.Y) / (DefaultTopPoint.X - topPoint.X)) * 180 / Math.PI;
                //}
                //else
                //{
                //    RotationAngle = (int)Math.Atan((DefaultTopPoint.Y - topPoint.Y) / (DefaultTopPoint.X - topPoint.X)) * 180 / Math.PI + 180;
                //}
            }

            Point[] QRrect = new Point[4];

            //计算二维码矩形的第四个点
            Point markPosotion = new Point();
            if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                //第一象限
                var topx = rectPoints[selecttop].MarkPoints.Min(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                var topy = rectPoints[selecttop].MarkPoints.Min(s => s.Y);
                QRrect[a] = new Point(topx, topy); //rectPoints[selecttop].MarkPoints.First(s => s.X == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var bx = marks.Max(m => m.X);
                            var by = marks.Min(m => m.Y);
                            QRrect[b] = new Point(bx, by);// marks.First(m => m.Y == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var dx = marks.Min(m => m.X);
                            var dy = marks.Max(m => m.Y);
                            QRrect[d] = new Point(dx, dy);//marks.First(m => m.Y == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                QRrect[c] = new Point(markPosotionx, markPosotiony);
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                //第二象限

                var topx = rectPoints[selecttop].MarkPoints.Max(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                var topy = rectPoints[selecttop].MarkPoints.Min(s => s.Y);
                QRrect[a] = new Point(topx, topy);//rectPoints[selecttop].MarkPoints.First(s => s.Y == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var cx = marks.Max(m => m.X);
                            var cy = marks.Max(m => m.Y);
                            QRrect[b] = new Point(cx, cy); //marks.First(m => m.Y == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var dx = marks.Min(m => m.X);
                            var dy = marks.Min(m => m.Y);
                            QRrect[d] = new Point(dx, dy);//marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - QRrect[b].X);
                var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[b].Y);
                QRrect[c] = new Point(markPosotionx, markPosotiony);
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                //第三象限

                var ax = rectPoints[selecttop].MarkPoints.Max(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                var ay = rectPoints[selecttop].MarkPoints.Max(s => s.Y);
                QRrect[a] = new Point(ax, ay); //rectPoints[selecttop].MarkPoints.First(s => s.Y == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var bx = marks.Min(m => m.X);
                            var by = marks.Max(m => m.Y);
                            QRrect[b] = new Point(bx, by); //marks.First(m => m.X == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var dx = marks.Max(m => m.X);
                            var dy = marks.Min(m => m.Y);
                            QRrect[d] = new Point(dx, dy); //marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                var markPosotiony = QRMatCenterPoint.Y - Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                QRrect[c] = new Point(markPosotionx, markPosotiony);
            }
            else if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                //第四象限

                var topx = rectPoints[selecttop].MarkPoints.Min(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                var topy = rectPoints[selecttop].MarkPoints.Max(s => s.Y);
                QRrect[a] = new Point(topx, topy); //rectPoints[selecttop].MarkPoints.First(s => s.X == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var dx = marks.Max(m => m.X);
                            var dy = marks.Max(m => m.Y);
                            QRrect[d] = new Point(dx, dy); //marks.First(m => m.X == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var bx = marks.Min(m => m.X);
                            var by = marks.Min(m => m.Y);
                            QRrect[b] = new Point(bx, by);//marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[a].X);
                var markPosotiony = QRMatCenterPoint.Y - Math.Abs(QRMatCenterPoint.Y - QRrect[a].Y);
                QRrect[c] = new Point(markPosotionx, markPosotiony);
            }

            wmax = Math.Max((int)Distance(QRrect[a], QRrect[c]), (int)Distance(QRrect[c], QRrect[d]));
            //Mat M = Cv2.GetRotationMatrix2D(QRMatCenterPoint, rectPoints[0].Angle, 1.0);
            //Mat dst = new Mat();
            //Cv2.WarpAffine(mat, dst, M, mat.Size(), InterpolationFlags.Cubic, BorderTypes.Replicate);
            //SaveMatFile(dst, "WarpAffine");
            Log.Information($"计算二维码边界坐标顺序：完成，坐标顺序：{QRrect[0].X},{QRrect[0].Y};{QRrect[1].X},{QRrect[1].Y};{QRrect[2].X},{QRrect[2].Y};{QRrect[3].X},{QRrect[3].Y}");
            return QRrect;
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