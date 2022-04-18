﻿using OpenCvSharp;
using OpenCvSharp.Internal.Vectors;
using QrCodeWeb.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Numerics;
using System.Runtime.Intrinsics;
using static System.Net.Mime.MediaTypeNames;
using Point = OpenCvSharp.Point;

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

            // Mat mat =
            Mat output = new Mat();
            Mat image = Cv2.ImRead(code, ImreadModes.Grayscale); //CV_8UC3
            Point2f[] points;
            IEnumerable<Point2f> resultPoints = new List<Point2f>();
            detector.Detect(image, out points);
            // var de = detector.Decode(image, resultPoints);

            var resule = detector.DetectAndDecode(image, out points, output);

            FindContours(image, points);
            SaveMatFile(output, "Detect");
            return "";
        }

        private Task FindContours(Mat srcmat, Point2f[] points)
        {

            Mat srcCopy = srcmat.Clone();
            // Mat result = new Mat(srcmat, new Rect(0, 0, points.s, points.Height));
            // Rect2f rect2F = new Rect2f(points.);

            // OutputArray<HierarchyIndex> hierarchy = new List<HierarchyIndex>();
            HierarchyIndex[] hierarchy ;
            Point[][] contours ;

            
           // VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();

            //VectorOfVectorOfPointF VectorOfPointF = new VectorOfVectorOfPointF();


            Cv2.FindContours(srcmat, out contours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            Mat canvas = new Mat(srcmat.Size(), MatType.CV_8UC3, Scalar.All(0));

           // Cv2.u
            //center_all获取特性中心
            List<Point[]> center_Point = new List<Point[]>();
            List<Point[]> contours2 = new List<Point[]>();
            Mat drawing = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);
            Mat drawing2 = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);
            Mat drawingAllContours = Mat.Zeros(srcmat.Size(), MatType.CV_8UC3);

            List<Point> center_all = new List<Point>();

            
            // 小方块的数量
            int numOfRec = 0;
            // 检测方块
            int ic = 0;
            int c = 0, k = 0, area = 0;

            //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
            int parentIdx = -1;
            for (int i = 0; i < contours.Length; i++)
            {
               // var ArcLength = 0.02 * Cv2.ArcLength(contours2[0], true);
                //画出所有轮廓图
                Cv2.DrawContours(drawingAllContours, contours, parentIdx, new Scalar(255, 255, 255));
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
                    var points2 = Cv2.ApproxPolyDP(contours[parentIdx], 20, true);
                    if (points2.Length == 4)
                    {
                        center_Point.Add(points2);
                    }
                  
                    //画出三个黑色定位角的轮廓
                    Cv2.DrawContours(drawing, contours, parentIdx, new Scalar(0, 125, 255), 1, LineTypes.Link8);
                 

                    if (IsQrPoint(contours[parentIdx], srcmat))
                    {
                        RotatedRect rectArea = Cv2.MinAreaRect(contours[parentIdx]);

                      //  画图部分
                       Point2f[] zpoints = new Point2f[4];
                        zpoints = rectArea.Points();
                        //for (int j = 0; j < 4; j++)
                        //{
                        //    line(src, points[j], points[(j + 1) % 4], Scalar(0, 255, 0), 2);
                        //}
                        Cv2.DrawContours(canvas, contours, parentIdx, new Scalar(0, 0, 255), -1);


                        //如果满足条件则存入
                        center_all.Add(rectArea.Center.ToPoint());
                        numOfRec++;
                    }
                    ic = 0;
                    parentIdx = -1;
                }

            }

            SaveMatFile(canvas, "canvas");

            SaveMatFile(drawing, "drawing");

           // IEnumerable<Point[]> points3 = new List<Point[]>();

           // points3.a
            //填充的方式画出三个黑色定位角的轮廓
            for (int i = 0; i < center_Point.Count; i++)
            {
              Cv2.DrawContours(drawing2, center_Point, i, new Scalar(122, 222, 111), -1, LineTypes.Link4, hierarchy, 0, new Point());
            }
            
            SaveMatFile(drawing2, "drawing2");
          

            // 连接三个正方形的部分
            //for (int i = 0; i < center_all.Count(); i++)
            //{
            //    Cv2.Line(canvas, center_all[i], center_all[(i + 1) % center_all.Count()], new Scalar(255, 0, 0), 3);
            //}

            Point[][] contours3 = new Point[0][];
            Mat canvasGray = new Mat();
            Cv2.CvtColor(canvas, canvasGray, ColorConversionCodes.BGR2GRAY);//COLOR_BGR2GRAY

            Cv2.FindContours(canvasGray, out contours3, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            Point[] maxContours = new Point[0];
            double maxArea = 0;

            // 计算“回”的次序关系
            int leftTopPointIndex = leftTopPoint(center_all.ToArray());
            int[] otherTwoPointIndex = otherTwoPoint(center_all.ToArray(), leftTopPointIndex);
            // canvas上标注三个“回”的次序关系
            Cv2.Circle(canvas, center_all[leftTopPointIndex], 10, new Scalar(255, 0, 255), -1);
            Cv2.Circle(canvas, center_all[otherTwoPointIndex[0]], 10, new Scalar(0, 255, 0), -1);
            Cv2.Circle(canvas, center_all[otherTwoPointIndex[1]], 10, new Scalar(0, 255, 255), -1);

            // 计算旋转角
            double angle = rotateAngle(center_all[leftTopPointIndex], center_all[otherTwoPointIndex[0]], center_all[otherTwoPointIndex[1]]);

            // 拿出之前得到的最大的轮廓,重新
            RotatedRect rect = Cv2.MinAreaRect(maxContours);
            
            Mat image = transformQRcode(srcCopy, rect, angle);

            SaveMatFile(image,"image");

            // 如果没有找到方块，则返回
            return Task.CompletedTask;
        }

        // 
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
            // 转化为灰度图像
           // Cv2.CvtColor(image, dstGray, ColorConversionCodes.BGR2GRAY);//COLOR_BGR2GRAY
            // 进行二值化

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
            int minIndex = 0;
            int multiple = 0;
            int minMultiple = 10000;
            multiple = (centerPoint[1].X - centerPoint[0].X) * (centerPoint[2].X - centerPoint[0].X) + (centerPoint[1].Y - centerPoint[0].Y) * (centerPoint[2].Y - centerPoint[0].Y);
            if (minMultiple > multiple)
            {
                minIndex = 0;
                minMultiple = multiple;
            }
            multiple = (centerPoint[0].X - centerPoint[1].X) * (centerPoint[2].X - centerPoint[1].X) + (centerPoint[0].Y - centerPoint[1].Y) * (centerPoint[2].Y - centerPoint[1].Y);
            if (minMultiple > multiple)
            {
                minIndex = 1;
                minMultiple = multiple;
            }
            multiple = (centerPoint[0].X - centerPoint[2].X) * (centerPoint[1].X - centerPoint[2].X) + (centerPoint[0].Y - centerPoint[2].Y) * (centerPoint[1].Y - centerPoint[2].Y);
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
            Point[][] contours = new Point[5][];
            contours.Append(contour);
            // 再中介图像中画出轮廓
            Cv2.DrawContours(mask, contours, 0, new Scalar(255, 255, 255), -1);
            // 通过mask掩膜将src中特定位置的像素拷贝到dst中。
            src.CopyTo(dst, mask);
            // 旋转
            Mat M = Cv2.GetRotationMatrix2D(center, angle, 1);
            Cv2.WarpAffine(dst, image, M, src.Size());

            // 截图
            Mat roi = new Mat(image, RoiRect);// image.g(RoiRect);

            return roi;
        }

        private Task SaveMatFile(Mat array,string name)
        {
            var filepath = Path.Combine(Environment.ContentRootPath, $"{name}.jpg");
            // Mat image = array.GetMat();

            //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
            array.SaveImage(filepath);

            return Task.CompletedTask;
        }
    }
}