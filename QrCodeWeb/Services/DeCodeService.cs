using OpenCvSharp;
using OpenCvSharp.Internal.Vectors;
using QrCodeWeb.Controllers;
using QrCodeWeb.Datas;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
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

        public string Decode(Mat img)
        {
            //var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
            //                                             _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

            //var src = Cv2.ImRead(@"code", ImreadModes.Grayscale);

            //string[] texts;
            //Mat[] rects;
            //wechatQrcode.DetectAndDecode(src, out rects, out texts);

            // Mat detectormat = new Mat(); string decodestring = "";

            // Mat src = new Mat(img, ImreadModes.Grayscale);

            //Mat grad_x = new Mat();
            //Mat grad_x2 = new Mat();
            //Cv2.Sobel(img, grad_x, MatType.CV_16S, 1, 0);
            //Cv2.ConvertScaleAbs(grad_x, grad_x2);

            //Mat grad_y = new Mat();
            //Mat grad_y2 = new Mat();
            //Cv2.Sobel(img, grad_y, MatType.CV_16S, 0, 1);
            //Cv2.ConvertScaleAbs(grad_y, grad_y2);

            //Mat resultm = new Mat();
            //Cv2.AddWeighted(grad_x2, 0.5, grad_y2, 0.5, 0, resultm);
            //// result.SaveImage(img_result);

            //Mat result2 = new Mat();
            //Mat result2Filter2D = new Mat();
            //Cv2.Laplacian(resultm, result2, MatType.CV_16S, 3);
            //Cv2.Filter2D(resultm, result2Filter2D, MatType.CV_16SC3, resultm);
            //SaveMatFile(img, "解析原图");
            //SaveMatFile(result2, "Laplacian解析处理图");
            //SaveMatFile(result2Filter2D, "Filter2D解析处理图");
            QRCodeDetector detector = new QRCodeDetector();

            Point2f[] pointfDetect;
            var b = detector.Detect(img, out pointfDetect);

            using Mat roi = new Mat();
            Point2f[] pointf;
            var result = detector.DetectAndDecode(img, out pointf, roi);
            if (result != "" || result is null)
            {
                return result;
            }

            // create a barcode reader instance
            //IBarcodeReader reader = new BarcodeReader<Mat>();
            BarcodeReader reader = new BarcodeReader();

            // load a bitmap var barcodeBitmap =
            // (Bitmap)Image.LoadFrom("C:\\sample-barcode-image.png"); detect and decode the barcode
            // inside the bitmap
            var resultreader = reader.Decode(img);
            // reader.Decode(img); do something with the result
            if (resultreader != null)
            {
                return result = resultreader.Text;
                // txtDecoderType.Text = result.BarcodeFormat.ToString();
                //txtDecoderContent.Text = result.Text;
            }
            else
            {
                result = "二维码解析失败";
            }

            return result;
        }

        public string DetectAndDecode(string code, ref ResponseModel response)
        {
            Mat image = Cv2.ImRead(code);

            Mat Preprocessing_mat = MatPreprocessing(image.Clone(), ref response);
            // SaveMatFile(Preprocessing_mat, "Preprocessing_mat");

            //二维码的三个定位点
            List<RectPoints> rectPoints = new List<RectPoints>();
            GetPosotionDetectionPatternsPoints(Preprocessing_mat.Clone(), out rectPoints);
            if (rectPoints.Count != 3)
            {
                response.Code = "-1";
                response.Message = "没有找到二维码";
                return "";
            }
            if (rectPoints.Count == 3)
            {
                //根据定位点获取完整二维码
                int moduleSize = 0;
                using Mat qrCodeAreaRectMat = GetQrCodeMat(image, rectPoints, out moduleSize);

                SaveMatFile(qrCodeAreaRectMat, "qrCodeAreaRectMat");
                //对完整二维码重新处理
                using Mat qrCodeAreaRectMatpre = MatPreprocessing(qrCodeAreaRectMat.Clone(), ref response, true);
                //二维码的三个定位点
                List<RectPoints> PatternsPoints = new List<RectPoints>();
                GetPosotionDetectionPatternsPoints(qrCodeAreaRectMatpre, out PatternsPoints);
                if (PatternsPoints.Count < 3)
                {
                    response.Code = "501";
                    response.Message = "无法识别二维码";
                    return "";
                }
                response.Code = "200";
                response.Message = "成功找到二维码";
                using Mat Patterns = GetPosotionDetectionPatternsMat(qrCodeAreaRectMat, PatternsPoints, moduleSize);
                SaveMatFile(Patterns, "Patterns");
            }

            // Cv2.CvtColor(areaRectMat, areaRectMat, ColorConversionCodes.BGR2GRAY);

            // Mat Patterns = GetPosotionDetectionPatternsMat(areaRectMat, moduleSize);

            // SaveMatFile(Patterns, "Patterns");

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
        private Mat MatPreprocessing(Mat srcmat, ref ResponseModel response, bool isdecode = false)
        {
            using Mat GRAY_mat = new Mat();
            //using Mat BilateralFilter = new Mat();
            using Mat ScaleAbs_mat = new Mat();
            Cv2.CvtColor(srcmat, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图

            // Cv2.BilateralFilter(GRAY_mat, BilateralFilter, 5, 10, 2);

            // Cv2.GaussianBlur(GRAY_mat, Blur_mat, new Size(3, 3), 1);//灰度图平滑处理
            Cv2.ConvertScaleAbs(GRAY_mat, ScaleAbs_mat, 3, 7);//图像增强对比度

            SaveMatFile(ScaleAbs_mat, "ScaleAbs_mat");

            Mat Threshold_mat = new Mat();
            Cv2.Threshold(ScaleAbs_mat, Threshold_mat, 0, 255, ThresholdTypes.Otsu);
            SaveMatFile(Threshold_mat, "Threshold_Binary");

            if (isdecode)
            {
                var code = Decode(Threshold_mat);
                response.Data = code;
            }
            using Mat Close_mat = new Mat();
            using Mat elementClose = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2, 2));
            Cv2.MorphologyEx(Threshold_mat, Close_mat, MorphTypes.Close, elementClose);
            SaveMatFile(Close_mat, "MorphologyEx_Close");

            //Mat Dilate_mat = new Mat();
            //Mat elementDilate = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(15, 15));
            //Cv2.MorphologyEx(Close_mat, Dilate_mat, MorphTypes.Dilate, elementDilate);
            //SaveMatFile(Dilate_mat, "MorphologyEx_Dilate");

            using Mat Erode_mat = new Mat();
            using Mat elemen3t = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
            Cv2.MorphologyEx(Close_mat, Erode_mat, MorphTypes.Erode, elemen3t);
            SaveMatFile(Erode_mat, "MorphologyEx_Erode");

            //Cv2.Canny(ScaleAbs_mat, Threshold_mat, 80, 200);
            Mat MorphologyEx_mat = new Mat();
            //第一个参数MORPH_RECT表示矩形的卷积核，当然还可以选择椭圆形的、交叉型
            using Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

            //对截取的图像进行闭运算，消除小型黑洞

            Cv2.MorphologyEx(Erode_mat, MorphologyEx_mat, MorphTypes.Gradient, element);
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
            using Mat drawingAllmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            using Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            // 轮廓圈套层数
            rectPoints = new List<RectPoints>();
            //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
            int ic = 0;
            int parentIdx = -1;
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
                if (ic >= 5)
                {
                    //保存找到的三个黑色定位角
                    var points2 = Cv2.ApproxPolyDP(matcontours[parentIdx], 25, true);
                    if (points2.Length == 4)
                    {
                        int ArcLength = (int)Cv2.ArcLength(matcontours[parentIdx], true);
                        RotatedRect rotated = Cv2.MinAreaRect(points2);
                        var rects = new RectPoints()
                        {
                            CenterPoints = rotated.Center.ToPoint(),
                            MarkPoints = points2,
                            Angle = rotated.Angle
                        };
                        rectPoints.Add(rects);
                        //画出三个黑色定位角的轮廓
                        Cv2.DrawContours(drawingmark, matcontours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                    }
                    //画出三个黑色定位角的轮廓
                    Cv2.DrawContours(drawingAllmark, matcontours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                }
            }
            SaveMatFile(drawingmark, "drawingmark");
            SaveMatFile(drawingAllmark, "drawingAllmark");
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

            Rect rect = new Rect(x, y, w, w);//取得9个模块宽度
            Mat roi = new Mat(mat, rect);
            return roi;
        }

        /// <summary>
        /// 返回二维码区域Rect
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private Point[] GetQrCodePoints(List<RectPoints> rectPoints, out int wmax, Mat mat)
        {
            //
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

            return QRrect;
        }

        //private Mat GetThreshold(Mat mat)
        //{
        //}

        private Task SaveMatFile(Mat array, string name)
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"testdata/{FileNmae}");

            if (!Directory.Exists(filepathw))
            {
                Directory.CreateDirectory(filepathw);
            }

            var filepath = Path.Combine(filepathw, $"{FileNmaeIdnex}_{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);

            FileNmaeIdnex++;
            return Task.CompletedTask;
        }
    }

    public class BarcodeReaderImage : BarcodeReader<Mat>, IBarcodeReaderImage
    {
        /// <summary>
        /// define a custom function for creation of a luminance source with our specialized
        /// Mat-supporting class
        /// </summary>
        private static readonly Func<Mat, LuminanceSource> defaultCreateLuminanceSource =
           (image) => new ImageLuminanceSource(image);

        /// <summary>
        /// constructor which uses a custom luminance source with Mat support
        /// </summary>
        public BarcodeReaderImage()
           : base(null, defaultCreateLuminanceSource, null)
        {
        }
    }

    internal interface IBarcodeReaderImage
    {
    }

    /// <summary>
    /// A luminance source class which consumes a Mat image from OpenCVSharp and calculates the
    /// luminance values based on the bytes of the image
    /// </summary>
    internal class ImageLuminanceSource : BaseLuminanceSource
    {
        public ImageLuminanceSource(Mat image)
           : base(image.Width, image.Height)
        {
            CalculateLuminance(image);
        }

        protected ImageLuminanceSource(byte[] luminances, int width, int height)
           : base(luminances, width, height)
        {
        }

        protected override LuminanceSource CreateLuminanceSource(byte[] newLuminances, int width, int height)
        {
            return new ImageLuminanceSource(newLuminances, width, height);
        }

        private void CalculateLuminance(Mat src)
        {
            if (src == null)
                throw new ArgumentNullException("src");
            if (src.Dims > 2)
                throw new ArgumentException("Mat dimensions must be 2");

            var pixelFormat = GetOptimumPixelFormats(src.Type());

            if (src.IsSubmatrix())
                throw new NotSupportedException("Submatrix");
            //if (src.IsContinuous())
            //   throw new NotSupportedException("Continuous");

            unsafe
            {
                byte* pSrc = (byte*)(src.Data);
                //int sstep = (int) src.Step();

                switch (pixelFormat)
                {
                    case RGBLuminanceSource.BitmapFormat.Gray8:
                        CalculateLuminanceGray8(pSrc, src.DataEnd.ToInt64() - src.DataStart.ToInt64());
                        break;

                    case RGBLuminanceSource.BitmapFormat.BGR24:
                        CalculateLuminanceBGR24(pSrc, src.DataEnd.ToInt64() - src.DataStart.ToInt64());
                        break;

                    case RGBLuminanceSource.BitmapFormat.BGRA32:
                        CalculateLuminanceBGRA32(pSrc, src.DataEnd.ToInt64() - src.DataStart.ToInt64());
                        break;
                }
            }
        }

        private static RGBLuminanceSource.BitmapFormat GetOptimumPixelFormats(MatType type)
        {
            if (type == MatType.CV_8UC1 || type == MatType.CV_8SC1)
                return RGBLuminanceSource.BitmapFormat.Gray8;
            if (type == MatType.CV_8UC3 || type == MatType.CV_8SC3)
                return RGBLuminanceSource.BitmapFormat.BGR24;
            if (type == MatType.CV_8UC4 || type == MatType.CV_8SC4)
                return RGBLuminanceSource.BitmapFormat.BGRA32;

            //if (type == MatType.CV_16UC1 || type == MatType.CV_16SC1)
            //   return RGBLuminanceSource.BitmapFormat.Gray16;
            //if (type == MatType.CV_16UC3 || type == MatType.CV_16SC3)
            //   return RGBLuminanceSource.BitmapFormat.Rgb48;
            //if (type == MatType.CV_16UC4 || type == MatType.CV_16SC4)
            //   return RGBLuminanceSource.BitmapFormat.Rgba64;

            //if (type == MatType.CV_32SC4)
            //   return RGBLuminanceSource.BitmapFormat.Prgba64;

            //if (type == MatType.CV_32FC1)
            //   return RGBLuminanceSource.BitmapFormat.Gray32Float;
            //if (type == MatType.CV_32FC3)
            //   return RGBLuminanceSource.BitmapFormat.Rgb128Float;
            //if (type == MatType.CV_32FC4)
            //   return RGBLuminanceSource.BitmapFormat.Rgba128Float;

            throw new ArgumentOutOfRangeException(type.GetType().Name, "Not supported MatType");
        }

        private unsafe void CalculateLuminanceGray8(byte* rgbRawBytes, long length)
        {
            for (int index = 0, luminanceIndex = 0; index < length && luminanceIndex < luminances.Length; luminanceIndex++)
            {
                // MemCopy should be faster
                luminances[luminanceIndex] = rgbRawBytes[index++];
            }
        }

        private unsafe void CalculateLuminanceBGR24(byte* rgbRawBytes, long length)
        {
            for (int rgbIndex = 0, luminanceIndex = 0; rgbIndex < length && luminanceIndex < luminances.Length; luminanceIndex++)
            {
                // Calculate luminance cheaply, favoring green.
                int b = rgbRawBytes[rgbIndex++];
                int g = rgbRawBytes[rgbIndex++];
                int r = rgbRawBytes[rgbIndex++];
                luminances[luminanceIndex] = (byte)((RChannelWeight * r + GChannelWeight * g + BChannelWeight * b) >> ChannelWeight);
            }
        }

        private unsafe void CalculateLuminanceBGRA32(byte* rgbRawBytes, long length)
        {
            for (int rgbIndex = 0, luminanceIndex = 0; rgbIndex < length && luminanceIndex < luminances.Length; luminanceIndex++)
            {
                // Calculate luminance cheaply, favoring green.
                var b = rgbRawBytes[rgbIndex++];
                var g = rgbRawBytes[rgbIndex++];
                var r = rgbRawBytes[rgbIndex++];
                var alpha = rgbRawBytes[rgbIndex++];
                var luminance = (byte)((RChannelWeight * r + GChannelWeight * g + BChannelWeight * b) >> ChannelWeight);
                luminances[luminanceIndex] = (byte)(((luminance * alpha) >> 8) + (255 * (255 - alpha) >> 8));
            }
        }
    }
}