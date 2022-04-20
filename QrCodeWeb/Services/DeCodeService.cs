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
using System.Runtime.Intrinsics;
using System.Threading;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace QrCodeWeb.Services
{
    public class DeCodeService
    {
        private IWebHostEnvironment Environment { get; }
        private readonly ILogger<DeCodeService> Logger;

        public DeCodeService(IWebHostEnvironment environment, ILogger<DeCodeService> logger)
        {
            Environment = environment;
            Logger = logger;
        }

        public string Decode(string code)
        {
            QRCodeDetector detector = new QRCodeDetector();

            Mat output = new Mat();
            Mat image = Cv2.ImRead(code); //CV_8UC3
            Point2f[] points;
            IEnumerable<Point2f> resultPoints = new List<Point2f>();
            //detector.Detect(image, out points);
            // var de = detector.Decode(image, resultPoints);

            var resule = detector.DetectAndDecode(image, out points, output);

            if (resule is "")
            {
                //解码失败
                DetectAndDecodeFail(image);
            }
            else
            {
                //解码成功
                RotatedRect rotatedRect = Cv2.MinAreaRect(points);
                Mat mat = new Mat(image, rotatedRect.BoundingRect());
                DetectAndDecodeOK(mat);
            }
            SaveMatFile(output, "Detect");
            return "";
        }

        private async Task<string> DetectAndDecodeFail(Mat srcmat)
        {
            Mat areaRectMat = GetQrCodeMat(srcmat);

            SaveMatFile(areaRectMat, "areaRectMat");

            // Cv2.CvtColor(areaRectMat, areaRectMat, ColorConversionCodes.BGR2GRAY);

            Mat Patterns = GetPosotionDetectionPatternsMat(areaRectMat);

            SaveMatFile(Patterns, "Patterns");

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

        private Mat GetQrCodeMat(Mat mat)
        {
            Mat Preprocessing_mat = MatPreprocessing(mat.Clone());
            // SaveMatFile(Preprocessing_mat, "Preprocessing_mat");

            //二维码的三个定位点
            List<RectPoints> rectPoints = new List<RectPoints>();
            HierarchyIndex[] hierarchy = GetPosotionDetectionPatternsPoints(Preprocessing_mat, out rectPoints);

            var width = 0; //Preprocessing_mat.Width;
            //根据轮廓坐标 计算二维码位置
            Point[] src = GetQrCodePoints(rectPoints, out width);

            Point2f[] src_coners = new Point2f[]
                {
                new Point2f(src[0].X,src[0].Y),
                    new Point2f(src[1].X,src[1].Y),
                    new Point2f(src[2].X,src[2].Y),
                    new Point2f(src[3].X,src[3].Y)
                };
            Rect rect = new Rect(20, 20, width + 20, width + 20);

            Point2f[] dst_coners = new Point2f[]
            {
                new Point2f(rect.X,rect.Y),
                new Point2f(width,rect.Y),
                new Point2f(width,width),
                new Point2f(rect.X,width),
            };

            Mat warpMatrix = Cv2.GetPerspectiveTransform(src_coners, dst_coners);

            Mat dst = new Mat(rect.Size, MatType.CV_8UC3);
            Cv2.WarpPerspective(mat, dst, warpMatrix, dst.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

            Point2f[] CenterPoints;// = new List<Point2f>();
            Mat output = new Mat();
            var de = new QRCodeDetector().DetectAndDecode(dst, out CenterPoints, output);
            //Rect rect = rotated.BoundingRect();
            //根据位置截图二维码区域
            SaveMatFile(dst, "WarpPerspective");

            return dst;
        }

        private Task DetectAndDecodeOK(Mat srcmat)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 图像预处理
        /// </summary>
        /// <param name="srcmat"></param>
        /// <returns></returns>
        private Mat MatPreprocessing(Mat srcmat)
        {
            Mat GRAY_mat = new Mat();
            Mat Blur_mat = new Mat();
            Mat ScaleAbs_mat = new Mat();
            Cv2.CvtColor(srcmat, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图
            Cv2.Blur(GRAY_mat, Blur_mat, new Size(5, 5));//灰度图平滑处理
            Cv2.ConvertScaleAbs(Blur_mat, ScaleAbs_mat, 2, 5);//图像增强对比度

            SaveMatFile(ScaleAbs_mat, "ScaleAbs_mat");

            Mat Threshold_mat = new Mat();
            Cv2.Threshold(ScaleAbs_mat, Threshold_mat, 128, 255, ThresholdTypes.Binary);
            SaveMatFile(Threshold_mat, "Threshold_mat");
            //Cv2.Canny(ScaleAbs_mat, Threshold_mat, 80, 200);
            Mat MorphologyEx_mat = new Mat();
            //第一个参数MORPH_RECT表示矩形的卷积核，当然还可以选择椭圆形的、交叉型
            Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

            //对截取的图像进行闭运算，消除小型黑洞
            Cv2.MorphologyEx(Threshold_mat, MorphologyEx_mat, MorphTypes.Gradient, element);
            SaveMatFile(MorphologyEx_mat, "MorphologyEx_mat");
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
        private HierarchyIndex[] GetPosotionDetectionPatternsPoints(Mat dilatemat, out List<RectPoints> rectPoints)
        {
            Point[][] matcontours;
            HierarchyIndex[] hierarchy;
            ///算出二维码轮廓
            Cv2.FindContours(dilatemat, out matcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            Mat drawing = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);

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
                    var points2 = Cv2.ApproxPolyDP(matcontours[parentIdx], 20, true);
                    if (points2.Length == 4)
                    {
                        RotatedRect rotated = Cv2.MinAreaRect(points2);
                        var rects = new RectPoints()
                        {
                            CenterPoints = rotated.Center.ToPoint(),
                            MarkPoints = points2
                        };

                        rectPoints.Add(rects);
                    }
                    //画出三个黑色定位角的轮廓
                    Cv2.DrawContours(drawing, matcontours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                }
            }
            SaveMatFile(drawingAllContours, "drawingAllContours");
            SaveMatFile(drawing, "drawing");
            return hierarchy;
        }

        private Mat GetPosotionDetectionPatternsMat(Mat mat)
        {
            Mat gay = MatPreprocessing(mat.Clone());
            //Mat gay = mat.Clone();

            //Mat gmat = Mat.Zeros(mat.Size(), MatType.CV_8UC1);
            //Cv2.CvtColor(gay, gmat, ColorConversionCodes.BGR2GRAY);

            //二维码的三个定位点
            List<RectPoints> rectPoints = new List<RectPoints>();

            HierarchyIndex[] hierarchy = GetPosotionDetectionPatternsPoints(gay, out rectPoints);

            var maxw = rectPoints.Max(w => w.CenterPoints.X);

            //  var maxH = rectPoints.Max(w => w.CenterPoints.Y);

            var prect = rectPoints.FindLast(f => f.CenterPoints.X == maxw);

            var minx = prect.MarkPoints.Min(m => m.X);
            // var maxH = rectPoints.Max(w => w.CenterPoints.Y);

            var ArcLength = Cv2.ArcLength(prect.MarkPoints, true);
            var le = (int)(ArcLength / 4);//单边长
            var px = (int)(le / 7);//二维码模块大小

            var x = (int)(minx - px * 2);
            var y = 1;

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
        private Point[] GetQrCodePoints(List<RectPoints> rectPoints, out int max)
        {
            //var width = rectPoints.Max(x => x.MarkPoints[0].X);
            int a = 0, b = 1, c = 2, d = 3;
            //计算三个定位点的距离
            int AB = (int)Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
            int BC = (int)Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
            int AC = (int)Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);
            //计算二维码的中心坐标,计算二维码定位点之间最长向量，二维码的中点为 最长向量的中心坐标。
            max = Math.Max(AB, Math.Max(BC, AC));
            Point topPoint = new Point();
            Point QRMatCenterPoint = new Point();
            int selecttop = 0;

            //
            if (max == AB)
            {
                topPoint = rectPoints[c].CenterPoints;//二维码直角顶点
                selecttop = 2;
                //计算二维码的中心坐标
                QRMatCenterPoint.X = (rectPoints[a].CenterPoints.X + rectPoints[b].CenterPoints.X) / 2;
                QRMatCenterPoint.Y = (rectPoints[a].CenterPoints.Y + rectPoints[a].CenterPoints.Y) / 2;
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

            Point[] QRrect = new Point[4];

            //计算二维码矩形的第四个点
            Point markPosotion = new Point();
            if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
            {
                //第一象限
                var topx = rectPoints[selecttop].MarkPoints.Min(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                QRrect[a] = rectPoints[selecttop].MarkPoints.First(s => s.X == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var miny = marks.Min(m => m.Y);
                            QRrect[b] = marks.First(m => m.Y == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var maxy = marks.Max(m => m.Y);
                            QRrect[d] = marks.First(m => m.Y == maxy);
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

                var topx = rectPoints[selecttop].MarkPoints.Min(s => s.Y);//FindPoint(rectPoints[selecttop].MarkPoints);
                QRrect[b] = rectPoints[selecttop].MarkPoints.First(s => s.Y == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var miny = marks.Max(m => m.Y);
                            QRrect[c] = marks.First(m => m.Y == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var maxy = marks.Min(m => m.X);
                            QRrect[a] = marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[b].X);
                var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[b].Y);
                QRrect[d] = new Point(markPosotionx, markPosotiony);

                //markPosotion.X = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - topPoint.X);
                //markPosotion.Y = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - topPoint.Y);
            }
            else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                //第三象限

                var topx = rectPoints[selecttop].MarkPoints.Max(s => s.Y);//FindPoint(rectPoints[selecttop].MarkPoints);
                QRrect[c] = rectPoints[selecttop].MarkPoints.First(s => s.Y == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var miny = marks.Min(m => m.X);
                            QRrect[d] = marks.First(m => m.X == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var maxy = marks.Max(m => m.X);
                            QRrect[b] = marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[c].X);
                var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[c].Y);
                QRrect[a] = new Point(markPosotionx, markPosotiony);

                //markPosotion.X = QRMatCenterPoint.X - Math.Abs(QRMatCenterPoint.X - topPoint.X);
                //markPosotion.Y = QRMatCenterPoint.Y - Math.Abs(QRMatCenterPoint.Y - topPoint.Y);
            }
            else if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
            {
                //第四象限

                var topx = rectPoints[selecttop].MarkPoints.Max(s => s.X);//FindPoint(rectPoints[selecttop].MarkPoints);
                QRrect[d] = rectPoints[selecttop].MarkPoints.First(s => s.X == topx);

                for (int i = 0; i < rectPoints.Count; i++)
                {
                    if (i != selecttop)
                    {
                        var marks = rectPoints[i].MarkPoints;
                        if (rectPoints[i].CenterPoints.Y > QRMatCenterPoint.Y)
                        {
                            var miny = marks.Max(m => m.Y);
                            QRrect[c] = marks.First(m => m.Y == miny);
                        }
                        if (rectPoints[i].CenterPoints.Y < QRMatCenterPoint.Y)
                        {
                            var maxy = marks.Min(m => m.X);
                            QRrect[a] = marks.First(m => m.X == maxy);
                        }
                    }
                }
                var markPosotionx = QRMatCenterPoint.X + Math.Abs(QRMatCenterPoint.X - QRrect[d].X);
                var markPosotiony = QRMatCenterPoint.Y + Math.Abs(QRMatCenterPoint.Y - QRrect[d].Y);
                QRrect[b] = new Point(markPosotionx, markPosotiony);
            }

            // List<Point2f> RectPoints = new List<Point2f>();
            max = Math.Max((int)Distance(QRrect[a], QRrect[c]), (int)Distance(QRrect[c], QRrect[d]));

            return QRrect;
        }

        private Task SaveMatFile(Mat array, string name)
        {
            var filepathw = Path.Combine(Environment.ContentRootPath, $"testdata");

            var filepath = Path.Combine(filepathw, $"{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);

            return Task.CompletedTask;
        }
    }
}