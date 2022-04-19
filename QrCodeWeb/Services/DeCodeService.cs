using OpenCvSharp;
using OpenCvSharp.Internal.Vectors;
using QrCodeWeb.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Numerics;
using System.Runtime.Intrinsics;
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
            Mat image = Cv2.ImRead(code, ImreadModes.Grayscale); //CV_8UC3
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

        private Task DetectAndDecodeFail(Mat srcmat)
        {
            Rect rect;
            Mat areaRectMat = GetQrCodeMat(srcmat, out rect);

            ///重新计算截取的区域二维码定位点的坐标
            List<Point[]> AllpatternsPoints;
            List<Point> CenterPoints;
            HierarchyIndex[] hierarchy2 = GetPosotionDetectionPatternsPoints(areaRectMat, out CenterPoints, out AllpatternsPoints);

            Mat areaRectMatdrawing = Mat.Zeros(areaRectMat.Size(), MatType.CV_8UC1);
            //填充的方式画出三个黑色定位角的轮廓
            for (int i = 0; i < AllpatternsPoints.Count; i++)
            {
                Cv2.DrawContours(areaRectMat, AllpatternsPoints, i, new Scalar(122, 222, 111), -1, LineTypes.Link4, hierarchy2, 0, new Point());
                Cv2.Line(areaRectMatdrawing, CenterPoints[i], CenterPoints[(i + 1) % CenterPoints.Count()], new Scalar(122, 222, 111), 3);
            }

            SaveMatFile(areaRectMat, "areaRectMat2");
            SaveMatFile(areaRectMatdrawing, "areaRectMatdrawing");

            /*以下代码只展示变换部分,其中ImageIn为输入图像,ImageOut为输出图像*/
            //变换前的3点
            var srcPoints = new Point2f[CenterPoints.Count];
            var dstPoints = new Point2f[CenterPoints.Count];
            for (int i = 0; i < CenterPoints.Count; i++)
            {
                srcPoints[i] = new Point2f(CenterPoints[i].X, CenterPoints[i].Y);
                var p = CenterPoints[i];
                if (i == 0)
                {
                    dstPoints[i] = new Point2f(0, 0);
                }
                if (i == 1)
                {
                    dstPoints[i] = new Point2f(rect.Width, 0);
                }
                if (i == 2)
                {
                    dstPoints[i] = new Point2f(0, rect.Width);
                }
            }

            ////变换后的3点
            //var dstPoints = new Point2f[] { new Point2f(CenterPoints[0].X, CenterPoints[0].Y),
            //                                new Point2f(CenterPoints[1].X, CenterPoints[1].Y),
            //                                new Point2f(CenterPoints[2].X, CenterPoints[2].Y),
            //                                 };

            //var dstPoints2 = new Point2f[] { new Point2f(0, 0),
            //                                new Point2f(0,rect1.Width),
            //                                new Point2f(CenterPoints[2].X, CenterPoints[2].Y),
            //                                 };
            ////根据变换前后四个点坐标,获取变换矩阵
            Mat mm = Cv2.GetAffineTransform(srcPoints, dstPoints);
            Mat WarpPerspective = new Mat();
            ////进行透视变换
            Cv2.WarpAffine(areaRectMat, WarpPerspective, mm, areaRectMat.Size());

            SaveMatFile(WarpPerspective, "WarpPerspective1212123123");
            //Mat AffineTransform = new Mat();
            //InputArray M = Cv2.GetAffineTransform(CenterPoints.ToList(), AffineTransform);

            // 连接三个正方形的部分
            //for (int i = 0; i < center_all.Count(); i++)
            //{
            //    Cv2.Line(canvas, center_all[i], center_all[(i + 1) % center_all.Count()], new Scalar(255, 0, 0), 3);
            //}

            // Point[][] contours3 = new Point[0][]; Mat canvasGray = new Mat();
            // Cv2.CvtColor(canvas, canvasGray, ColorConversionCodes.BGR2GRAY);//COLOR_BGR2GRAY

            //Cv2.FindContours(canvasGray, out contours3, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            //Mat thresholdmat = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);
            //Mat src = new Mat(srcmat, rect1);
            //Cv2.Threshold(src, thresholdmat, 128, 255, ThresholdTypes.BinaryInv);
            //Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(15, 15)); //第一个参数MORPH_RECT表示矩形的卷积核，当然还可以选择椭圆形的、交叉型的
            //Mat matDilate = new Mat();
            //Cv2.Dilate(thresholdmat, matDilate, element);

            //SaveMatFile(matDilate, "matDilate");

            //Point[][] maxContours;
            //Point[] MaxAreaRectContours = new Point[1];

            //double maxArea = 0;
            //Cv2.FindContours(matDilate, out maxContours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            //Mat matDilatess = Mat.Zeros(matDilate.Size(), MatType.CV_8UC3);
            //for (int i = 0; i < maxContours.Length; i++)
            //{
            //    Cv2.DrawContours(matDilate, maxContours, i, new Scalar(255, 0, 0), -1, LineTypes.Link4, hierarchy, 0, new Point());

            //    RotatedRect rotated = Cv2.MinAreaRect(maxContours[i]);
            //    Point2f[] points;
            //    points = rotated.Points();
            //    for (int j = 0; j < 4; j++)
            //    {
            //        // var p = points[j].ToPoint();
            //        Cv2.Line(matDilatess, points[j].ToPoint(), points[(j + 1) % 4].ToPoint(), new Scalar(0, 255, 0), 2);
            //    }
            //    var Area = Cv2.ContourArea(maxContours[i]);
            //    if (Area > maxArea)
            //    {
            //        MaxAreaRectContours = maxContours[i];
            //        maxArea = Area;
            //    }
            //}
            //SaveMatFile(matDilate, "areaRectMatdrawing1");
            //SaveMatFile(matDilatess, "areaRectMatdrawing2");
            //确定那个定位点 在直角位置上
            int leftTopPointIndex = leftTopPoint(CenterPoints.ToArray());

            // 计算“回”定位点 的次序关系
            int[] otherTwoPointIndex = otherTwoPoint(CenterPoints.ToArray(), leftTopPointIndex);

            // canvas上标注三个“回”的次序关系
            Cv2.Circle(areaRectMatdrawing, CenterPoints[leftTopPointIndex], 10, new Scalar(107, 142, 35), -1);
            Cv2.Circle(areaRectMatdrawing, CenterPoints[otherTwoPointIndex[0]], 20, new Scalar(122, 255, 255), -1);
            Cv2.Circle(areaRectMatdrawing, CenterPoints[otherTwoPointIndex[1]], 30, new Scalar(153, 50, 204), -1);

            SaveMatFile(areaRectMatdrawing, "areaRectMatdrawing3");
            // 计算旋转角
            double angle = rotateAngle(CenterPoints[leftTopPointIndex], CenterPoints[otherTwoPointIndex[0]], CenterPoints[otherTwoPointIndex[1]]);

            // 拿出之前得到的最大的轮廓,重新
            //RotatedRect rect = Cv2.MinAreaRect(MaxAreaRectContours);

            //Mat image = transformQRcode(areaRectMat, rect, angle);

            //SaveMatFile(image, "image");

            // 如果没有找到方块，则返回
            return Task.CompletedTask;
        }

        private Mat GetQrCodeMat(Mat mat, out Rect rect)
        {
            Mat dilatemat = MatPreprocessing(mat.Clone());
            SaveMatFile(dilatemat, "dilatemat");
            //二维码的三个定位点
            List<Point[]> centerPoint;
            List<Point> centerall;
            HierarchyIndex[] hierarchy = GetPosotionDetectionPatternsPoints(dilatemat, out centerall, out centerPoint);

            rect = new Rect();
            if (centerall is null || centerall.Count == 0) return new Mat();
            //根据轮廓坐标 计算二维码位置
            rect = GetQrCodeRect(centerPoint);
            //根据位置截图二维码区域
            return new Mat(dilatemat, rect);
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
            Mat thresholdmat = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);
            Cv2.Threshold(srcmat, thresholdmat, 128, 255, ThresholdTypes.Binary);
            SaveMatFile(thresholdmat, "thresholdmat");

            Mat dilatemat = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);
            //第一个参数MORPH_RECT表示矩形的卷积核，当然还可以选择椭圆形的、交叉型
            Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

            //对截取的图像进行闭运算，消除小型黑洞
            Cv2.MorphologyEx(thresholdmat, dilatemat, MorphTypes.Dilate, element);
            return dilatemat;
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
        private HierarchyIndex[] GetPosotionDetectionPatternsPoints(Mat dilatemat, out List<Point> CenterPoints, out List<Point[]> PatternsPoints)
        {
            Point[][] rotatedcontours;
            HierarchyIndex[] hierarchy;
            ///算出二维码轮廓
            Cv2.FindContours(dilatemat, out rotatedcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            Mat canvas = new Mat(dilatemat.Size(), MatType.CV_8UC3, Scalar.All(0));

            //二维码的三个定位点
            PatternsPoints = new List<Point[]>();
            //二维码的三个定位点的中心点
            CenterPoints = new List<Point>();

            Mat drawing = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
            Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);

            // 小方块的数量
            int numOfRec = 0;
            // 检测方块
            int ic = 0;
            int c = 0, k = 0, area = 0;

            //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
            int parentIdx = -1;
            for (int i = 0; i < rotatedcontours.Length; i++)
            {
                //画出所有轮廓图
                Cv2.DrawContours(drawingAllContours, rotatedcontours, parentIdx, new Scalar(255, 255, 255));
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
                if (ic >= 2)
                {
                    //保存找到的三个黑色定位角
                    var points2 = Cv2.ApproxPolyDP(rotatedcontours[parentIdx], 10, true);
                    if (points2.Length == 4)
                    {
                        PatternsPoints.Add(points2);
                        RotatedRect rotated = Cv2.MinAreaRect(points2);
                        CenterPoints.Add(rotated.Center.ToPoint());//获取轮廓的中心点
                    }

                    //画出三个黑色定位角的轮廓
                    Cv2.DrawContours(drawing, rotatedcontours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                    ic = 0;
                    parentIdx = -1;
                }
            }

            SaveMatFile(drawingAllContours, "drawingAllContours");
            SaveMatFile(drawing, "drawing");
            return hierarchy;
        }

        /// <summary>
        /// 返回二维码区域Rect
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private Rect GetQrCodeRect(List<Point[]> points)
        {
            if (points is null) return new Rect();
            Rect rect = new Rect();
            var point1 = points[0][0];
            var point2 = points[1][0];
            var point3 = points[2][0];
            int tempx = point1.X;
            int tempy = point1.Y;
            for (int i = 0; i < points.Count(); i++)
            {
                var point = points[i];
                for (int k = 0; k < point.Length; k++)
                {
                    if (tempx > point[k].X)
                    {
                        tempx = point[k].X;
                    }
                    if (tempy > point[k].Y)
                    {
                        tempy = point[k].Y;
                    }
                }
            }

            var Width1 = Math.Sqrt(Math.Abs(point1.X - point2.X) * Math.Abs(point1.X - point2.X) + Math.Abs(point1.Y - point2.Y) * Math.Abs(point1.Y - point2.Y));
            var Width2 = Math.Sqrt(Math.Abs(point1.X - point3.X) * Math.Abs(point1.X - point3.X) + Math.Abs(point1.Y - point3.Y) * Math.Abs(point1.Y - point3.Y));
            var Width3 = Math.Sqrt(Math.Abs(point2.X - point3.X) * Math.Abs(point2.X - point3.X) + Math.Abs(point2.Y - point3.Y) * Math.Abs(point2.Y - point3.Y));

            var maxWidth = Math.Max(Width1, Width2);
            maxWidth = Math.Max(maxWidth, Width3);

            rect.X = (int)(tempx - maxWidth / 3);
            rect.Y = (int)(tempy - maxWidth / 3);
            rect.Width = (int)(maxWidth + maxWidth);
            rect.Height = (int)(maxWidth + maxWidth);

            return rect;
        }

        /// <summary>
        /// 该部分用于检测是否是角点，与下面两个函数配合/（判断面积，面积太小，返回false, 在获取最小区域） -- 可以放弃
        /// </summary>
        /// <param name="contour"></param>
        /// <param name="img"></param>
        /// <returns></returns>
        private bool IsQrPoint(Point[] contour, Mat img)
        {
            double area = Cv2.ContourArea(contour);
            // 角点不可以太小
            if (area < 30)
                return false;
            RotatedRect rect = Cv2.MinAreaRect(contour);
            double w = rect.Size.Width;
            double h = rect.Size.Height;
            double rate = Math.Min(w, h) / Math.Max(w, h);
            if (rate > 0.7)
            {
                Mat outputarray = new Mat();
                // 返回旋转后的图片，用于把“回”摆正，便于处理
                Cv2.Transform(img, outputarray, rate);
                if (isCorner(outputarray))
                {
                    return true;
                }
            }
            return false;
        }

        // 用于判断是否属于角上的正方形-- 可以放弃
        private bool isCorner(Mat image)
        {
            // 定义mask
            Mat imgCopy = new Mat();
            Mat dstCopy = new Mat();
            Mat dstGray = image.Clone();
            imgCopy = image.Clone();
            // 转化为灰度图像 Cv2.CvtColor(image, dstGray, ColorConversionCodes.BGR2GRAY);//COLOR_BGR2GRAY 进行二值化

            Cv2.Threshold(dstGray, dstGray, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            dstCopy = dstGray.Clone();  //备份

            // 找到轮廓与传递关系 vector<vector<Point>> contours;
            Point[][] contours;

            HierarchyIndex[] hierarchy = new HierarchyIndex[0];
            Cv2.FindContours(dstCopy, out contours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            for (int i = 0; i < contours.Length; i++)
            {
                //cout << i << endl;
                var hierarchyVec4i = hierarchy[i].ToVec4i();
                if (hierarchyVec4i[2] == -1 && hierarchyVec4i[3] > 0)
                {
                    Rect rect = Cv2.BoundingRect(contours[i]);
                    // Rectangle rectangle = new Rectangle(image, rect, Scalar(0, 0, 255), 2);

                    Cv2.Rectangle(image, rect, new Scalar(0, 0, 255), 2);
                    // 最里面的矩形与最外面的矩形的对比
                    if (rect.Width < imgCopy.Cols * 2 / 7)      //2/7是为了防止一些微小的仿射
                        continue;
                    if (rect.Height < imgCopy.Rows * 2 / 7)      //2/7是为了防止一些微小的仿射
                        continue;
                    // 判断其中黑色与白色的部分的比例
                    if (Rate(dstGray) > 0.20)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // 计算内部所有白色部分占全部的比率
        private double Rate(Mat count)
        {
            int number = 0;
            int allpixel = 0;
            for (int row = 0; row < count.Rows; row++)
            {
                for (int col = 0; col < count.Cols; col++)
                {
                    if (count.At<UInt64>(row, col) == 255)
                    {
                        number++;
                    }
                    allpixel++;
                }
            }
            //cout << (double)number / allpixel << endl;
            return (double)number / allpixel;
        }

        /// <summary>
        /// 根据向量a=(x1,y1),b=(x2,y2) ,a⊥b的充要条件是a·b=0,即(x1x2+y1y2)=0，矫正二维码位置
        /// </summary>
        /// <param name="centerPoint"></param>
        /// <returns></returns>
        private int leftTopPoint(Point[] centerPoint)
        {
            Point p0 = centerPoint[0];
            Point p1 = centerPoint[1];
            Point p2 = centerPoint[2];
            int minIndex = 0;
            int multiple = 0;
            int minMultiple = 10000;
            multiple = (p1.X - p0.X) * (p2.X - p0.X) + (p1.Y - p0.Y) * (p2.Y - p0.Y);
            if (minMultiple > multiple)
            {
                minIndex = 0;
                minMultiple = multiple;
            }
            multiple = (p0.X - p1.X) * (p2.X - p1.X) + (p0.Y - p1.Y) * (p2.Y - p1.Y);
            if (minMultiple > multiple)
            {
                minIndex = 1;
                minMultiple = multiple;
            }
            multiple = (p0.X - p2.X) * (p1.X - p2.X) + (p0.Y - p2.Y) * (p1.Y - p2.Y);
            if (minMultiple > multiple)
            {
                minIndex = 2;
                minMultiple = multiple;
            }
            return minIndex;
        }

        private int[] otherTwoPoint(Point[] centerPoint, int leftTopPointIndex)
        {
            List<int> otherIndex = new List<int>();
            double waiji = (centerPoint[(leftTopPointIndex + 1) % 3].X - centerPoint[(leftTopPointIndex) % 3].X) *
                (centerPoint[(leftTopPointIndex + 2) % 3].Y - centerPoint[(leftTopPointIndex) % 3].Y) -
                (centerPoint[(leftTopPointIndex + 2) % 3].X - centerPoint[(leftTopPointIndex) % 3].X) *
                (centerPoint[(leftTopPointIndex + 1) % 3].Y - centerPoint[(leftTopPointIndex) % 3].Y);
            if (waiji > 0)
            {
                otherIndex.Add((leftTopPointIndex + 1) % 3);
                otherIndex.Add((leftTopPointIndex + 2) % 3);
            }
            else
            {
                otherIndex.Add((leftTopPointIndex + 2) % 3);
                otherIndex.Add((leftTopPointIndex + 1) % 3);
            }
            return otherIndex.ToArray();
        }

        /// <summary>
        /// 返回需要矫正的旋转角度
        /// </summary>
        /// <param name="leftTopPoint"></param>
        /// <param name="rightTopPoint"></param>
        /// <param name="leftBottomPoint"></param>
        /// <returns></returns>
        private double rotateAngle(Point leftTopPoint, Point rightTopPoint, Point leftBottomPoint)
        {
            double dy = rightTopPoint.Y - leftTopPoint.Y;
            double dx = rightTopPoint.X - leftTopPoint.X;
            double k = dy / dx;
            double angle = Math.Atan(k) * 180 / Math.PI;//转化角度
            if (leftBottomPoint.Y < leftTopPoint.Y)
                angle -= 180;
            return angle;
        }

        private Mat transformQRcode(Mat src, RotatedRect rect, double angle)
        {
            // 获得旋转中心
            Point center = rect.Center.ToPoint();
            // 获得左上角和右下角的角点，而且要保证不超出图片范围，用于抠图
            Point TopLeft = new Point(center.X, center.Y) - new Point(rect.Size.Height / 2, rect.Size.Width / 2);  //旋转后的目标位置
            TopLeft.X = TopLeft.X > src.Cols ? src.Cols : TopLeft.X;
            TopLeft.X = TopLeft.X < 0 ? 0 : TopLeft.X;
            TopLeft.Y = TopLeft.Y > src.Rows ? src.Rows : TopLeft.Y;
            TopLeft.Y = TopLeft.Y < 0 ? 0 : TopLeft.Y;

            int after_width, after_height;
            if (TopLeft.X + rect.Size.Width > src.Cols)
            {
                after_width = (int)(src.Cols - TopLeft.X - 1);
            }
            else
            {
                after_width = (int)rect.Size.Width - 1;
            }
            if (TopLeft.Y + rect.Size.Height > src.Rows)
            {
                after_height = (int)(src.Rows - TopLeft.Y - 1);
            }
            else
            {
                after_height = (int)rect.Size.Height - 1;
            }
            // 获得二维码的位置
            Rect RoiRect = new Rect((int)TopLeft.X, (int)TopLeft.Y, after_width, after_height);

            // dst是被旋转的图片，roi为输出图片，mask为掩模
            Mat mask = new Mat();

            Mat dst = new Mat();

            Mat image = new Mat();
            // 建立中介图像辅助处理图像

            Point[] contour = new Point[4];
            // 获得矩形的四个点
            Point2f[] points = new Point2f[4];
            points = rect.Points();
            for (int i = 0; i < 4; i++)
                contour[i] = points[i].ToPoint();

            //vector<vector<Point>> contours;
            List<Point[]> contours = new List<Point[]>();
            contours.Add(contour);

            // 再中介图像中画出轮廓
            Cv2.DrawContours(mask, contours, 0, new Scalar(255, 255, 255), -1);

            // SaveMatFile(mask, "mask"); 通过mask掩膜将src中特定位置的像素拷贝到dst中。
            src.CopyTo(dst, mask);
            // 旋转
            Mat M = Cv2.GetRotationMatrix2D(center, angle, 1);

            Cv2.WarpAffine(dst, image, M, src.Size());

            // 截图
            Mat roi = new Mat(image, RoiRect);// image.g(RoiRect);

            return roi;
        }

        private Task SaveMatFile(Mat array, string name)
        {
            var filepath = Path.Combine(Environment.ContentRootPath, $"{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);

            return Task.CompletedTask;
        }
    }
}