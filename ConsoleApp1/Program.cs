// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;

Console.WriteLine("Hello, World!");
int k = 0;
List<string> files = new List<string>();

var path = Console.ReadLine();

GetFiles(@"C:\Users\q4528\Desktop\测试数据\甄品保图像检测", ref files);

int OK = 0;

for (int i = 0; i < files.Count; i++)
{
    DateTime date = DateTime.Now;
    var file = files[i];
   // var file = @"C:\Users\q4528\Desktop\测试数据\定位\22.jpg";
    var filename = Path.GetFileName(file).Replace(".jpg", "");
    using Mat Src = Cv2.ImRead(file);
    Mat GRAY_mat;
    SaveMatFile(Src, $"原图", filename);


    //Cv2.CvtColor(Src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图
    //SaveMatFile(GRAY_mat, $"GRAY_mat", filename);
    int wsize = -1;
    var qrcode = Decode(Src, filename, out GRAY_mat, out wsize);

    ImgeWarpPerspective(GRAY_mat, i);

    continue;
    //if (qrcode is not "")
    //{
    //    Mat mat = HoughLines(Src, wsize, filename);
    //}
    //k = 0;
    //continue;

    if (GRAY_mat is not null)
    {
        Mat w = WarpAffine(GRAY_mat, filename);

        List<RectPoints> rectPoints = await MatPreprocessing(w.Clone(), filename);

        Point2f center;
        int rotationAngle = GetRotationAngle(rectPoints, out center);

        Mat m = Cv2.GetRotationMatrix2D(center, rotationAngle, 1);
        Mat dst = new Mat();
        Cv2.WarpAffine(w, dst, m, dst.Size());
        SaveMatFile(dst, $"矫正之后的原图", filename);

        Mat PatternsMat = GetPosotionDetectionPatternsMat(dst, rectPoints);
        SaveMatFile(PatternsMat, $"指定定位点", filename);

        if (rectPoints.Count >= 3)
        {
            OK++;
        }
        Console.WriteLine($"{i + 1}:{Difference(date)}:完成{filename}：{qrcode},找到定点:{rectPoints.Count};旋转角度:{rotationAngle}");
    }
    else
    {
        Console.WriteLine($"{i + 1}:{Difference(date)}：完成{filename}：{qrcode}-- 找不到二维码");
    }
    k = 0;
}

var b = Math.Round((double)OK / (double)files.Count, 3) * 100;
Console.WriteLine($"共：{files.Count}；成功数量：{OK};成功率：{b}%");

async Task<List<RectPoints>> MatPreprocessing(Mat src, string filename)
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

    //Mat elementDilate = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
    //Mat Dilate = new Mat();
    //Cv2.Dilate(GRAY_mat, Dilate, elementDilate);
    //SaveMatFile(Dilate, $"Dilate", filename);

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
    List<RectPoints> rectPoints = GetPosotionDetectionPatternsPoints(MorphologyEx_Erode, filename);

    return rectPoints;
}
double Difference(DateTime date)
{
    return (DateTime.Now - date).TotalSeconds;
}

Task GetFiles(string path, ref List<string> files)
{
    try
    {
        string[] filePaths = Directory.GetFiles(path);
        foreach (string filePath in filePaths)
        {
            files.Add(filePath);
        }
        string[] directoryPaths = Directory.GetDirectories(path);
        foreach (string directoryPath in directoryPaths)
        {
            GetFiles(directoryPath, ref files);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
    return Task.CompletedTask;
}

Mat WarpAffine(Mat roi, string filename)
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
    SaveMatFile(Erode, $"Erode", filename);
    //

    Mat Threshold_mat = new Mat();
    Cv2.Threshold(Erode, Threshold_mat, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
    SaveMatFile(Threshold_mat, "WarpAffine_Threshold二值化", filename);

    //Mat Threshold_mat11 = new Mat();
    //Cv2.AdaptiveThreshold(Erode, Threshold_mat11,  255, AdaptiveThresholdTypes.MeanC,ThresholdTypes.BinaryInv,101,2);
    //SaveMatFile(Threshold_mat11, "WarpAffine_AdaptiveThreshold二值化", filename);

    Mat elementDilate1 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
    Mat Dilatedst = new Mat();
    Cv2.Dilate(Threshold_mat, Dilatedst, elementDilate1);

    SaveMatFile(Dilatedst, "二维码膨胀边界图像", filename);

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
    SaveMatFile(mat, "二维码膨胀边界图像轮廓", filename);

    for (int i = 0; i < tp.Length; i++)
    {
        drawsrc.PutText(i.ToString(), tp[i], HersheyFonts.HersheySimplex, 2, new Scalar(255), 2);
        drawsrc.Line(tp[i], tp[(i + 1) % 4], new Scalar(255), 2);
    }
    SaveMatFile(drawsrc, "圈定的二维码边界", filename);

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

Task SaveMatFile(Mat array, string name, string FileNmae, int i = 0)
{
    var filepathw = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"testdata");
    if (!Directory.Exists(filepathw))
    {
        Directory.CreateDirectory(filepathw);
    }

    var filepathw2 = Path.Combine(filepathw, $"{FileNmae}");

    if (!Directory.Exists(filepathw2))
    {
        Directory.CreateDirectory(filepathw2);
    }

    var filepath = Path.Combine(filepathw2, $"{k}_{name}.jpg");
    // Mat image = array.GetMat();
    k++;
    //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
    array.SaveImage(filepath);
    return Task.CompletedTask;
}

List<RectPoints> GetPosotionDetectionPatternsPoints(Mat dilatemat, string name)
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
            var area2 = (int)Cv2.ContourArea(matcontours[i], false);
            if (area2 <= minAreaSzie || area2 >= maxAreaSzie) { continue; }

            var approxPolyDP = Cv2.ApproxPolyDP(matcontours[i], 0.03 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);//

            if (approxPolyDP.Length != 4)
            {
                //Cv2.DrawContours(drawing, matcontours, i, new Scalar(0, 125, 255), 4, LineTypes.Link8);
                //SaveMatFile(drawing, "无法逼近的轮廓", name);
                continue;
            }
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

List<RectPoints> GetPatternsPoints(Mat dilatemat, string name)
{
    try
    {
        List<RectPoints> rectPoints = new List<RectPoints>();
        Point[][] matcontours;
        HierarchyIndex[] hierarchy;
        ///算出二维码轮廓
        ///
        dilatemat =dilatemat.Canny(100,100);
        Cv2.FindContours(dilatemat, out matcontours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

        using Mat drawingmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
        using Mat drawingmarkminAreaSzie = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
        using Mat drawingAllmark = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
        using Mat drawingAllContours = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);

        using Mat drawing = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
        using Mat drawingf = Mat.Zeros(dilatemat.Size(), MatType.CV_8UC3);
        // 轮廓圈套层数
        rectPoints = new List<RectPoints>();

        //double moduleSzie = dilatemat.Width / 31;
        //double minAreaSzie = (moduleSzie * 4) * (moduleSzie * 4);
        //double maxAreaSzie = (moduleSzie * 12) * (moduleSzie * 12);
        //通过黑色定位角作为父轮廓，有两个子轮廓的特点，筛选出三个定位角
        int ic = 0;
        int parentIdx = -1;
        for (int i = 0; i < matcontours.Length; i++)
        {
            var area2 = (int)Cv2.ContourArea(matcontours[i], false);
            if (area2 <= 300) { continue; }

            int k = i;
            int c = 0;
            while (hierarchy[k].Child != -1){ k = hierarchy[k].Child; c++; }
            if (hierarchy[k].Child != -1){  c++; }
            if (c >= 5){
                var points2 = Cv2.ApproxPolyDP(matcontours[i], 0.02 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);//
                if (points2.Length == 4)
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
                        SaveMatFile(drawingmark, "三个定位点轮廓", name);
                    }
                    continue;
                }
            }
            Cv2.DrawContours(drawingAllContours, matcontours, i, new Scalar(255, 255, 255));
            SaveMatFile(drawingAllContours, "所有轮廓", name);


        }

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
///微信二维码解析
string Decode(Mat src, string filename, out Mat dst, out int wsize)
{
    string _wechat_QCODE_detector_prototxt_path = "WechatQrCodeFiles/detect.prototxt";
    string _wechat_QCODE_detector_caffe_model_path = "WechatQrCodeFiles/detect.caffemodel";
    string _wechat_QCODE_super_resolution_prototxt_path = "WechatQrCodeFiles/sr.prototxt";
    string _wechat_QCODE_super_resolution_caffe_model_path = "WechatQrCodeFiles/sr.caffemodel";

    var wechatQrcode = WeChatQRCode.Create(_wechat_QCODE_detector_prototxt_path, _wechat_QCODE_detector_caffe_model_path,
                                                 _wechat_QCODE_super_resolution_prototxt_path, _wechat_QCODE_super_resolution_caffe_model_path);

    string[] texts;
    Mat[] rects;
    wechatQrcode.DetectAndDecode(src, out rects, out texts);

    // wechatQrcode.
    wsize = -1;
    if (texts.Length <= 0)
    {
        dst = null;
        return "";
    }
    //wsize = (int)rects[0].Width;

    Mat drawingmark = src.Clone();
    List<Point[]> lpoint = new();
    //for (int i = 0; i < rects.Length; i++)
    //{
    ///获取二维码在原图中的四个坐标点
    var ss = rects[0];
    OutputArray array = ss;
    Point pt1 = new Point((int)ss.At<float>(0, 0), (int)ss.At<float>(0, 1));
    Point pt2 = new Point((int)ss.At<float>(1, 0), (int)ss.At<float>(1, 1));
    Point pt3 = new Point((int)ss.At<float>(2, 0), (int)ss.At<float>(2, 1));
    Point pt4 = new Point((int)ss.At<float>(3, 0), (int)ss.At<float>(3, 1));
    lpoint.Add(new Point[] { pt1, pt2, pt3, pt4 });

    Cv2.DrawContours(drawingmark, lpoint.ToArray(), 0, new Scalar(0, 125, 255), 3, LineTypes.Link8);
    SaveMatFile(drawingmark, "二维码位置", filename);
    ///设置截图范围Rect
    RotatedRect rotatedRect = ss.MinAreaRect();
    var center = rotatedRect.Center;
    var size2F = rotatedRect.Size;
    var MAX = Math.Min(size2F.Width, size2F.Height);

    //src.DrawContours(lpoint.ToArray(), 0, new Scalar(0, 125, 255), 3, LineTypes.Link8);

    //SaveMatFile(src, "二维码截取", filename);
    //wsize = (int)MAX;
    #region  //博瑞达 二维码截图范围
    //var RoiSize = new Size(MAX + (MAX / 2), MAX + (MAX / 2));
    //var RoiPoint = new Point(center.X - (MAX / 2) - (MAX / 4), center.Y - (MAX / 2) - (MAX / 4));

    //Rect rectroi = new Rect(RoiPoint, RoiSize);
    //dst = new Mat(src, rectroi);
#endregion

    #region  //甄品保二维码截图范围
    var zRoiSize = new Size(MAX * 2, MAX * 2);
    var zRoiPoint = new Point(center.X - MAX, center.Y - MAX);

    Rect zrectroi = new Rect(zRoiPoint, zRoiSize);
    dst = new Mat(src, zrectroi);
    #endregion

    SaveMatFile(dst, "二维码截取", filename);
    //   warpAffine(dst, image, M, sz);
    return texts[0];
}
double Distance(Point2f a, Point2f b)
{
    var selfx = Math.Abs(a.X - b.X);
    var selfy = Math.Abs(a.Y - b.Y);
    var selflen = Math.Sqrt((selfx * selfx) + (selfy * selfy));
    return selflen;
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

Point[] GetRoiPoint(List<RectPoints> rectPoints, float roiscale=2.0f, Rect? rect=null)
{

    if (roiscale > 3.0f)
    {
        roiscale = 3.0f;
    }
    if (roiscale < 1.5f)
    {
        roiscale = 1.5f;
    }
    //Log.Information($"计算二维码边界坐标顺序：开始");
    //var width = rectPoints.Max(x => x.MarkPoints[0].X);
    int a = 0, b = 1, c = 2, d = 3;
    //计算三个定位点的距离
    int AB = (int)Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
    int BC = (int)Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
    int AC = (int)Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);
    //计算二维码的中心坐标,计算二维码定位点之间最长向量，二维码的中点为 最长向量的中心坐标。
    var max = Math.Max(AB, Math.Max(BC, AC));

    //var ab = Point.Distance(rectPoints[a].CenterPoints, rectPoints[b].CenterPoints);
    //var bc = Point.Distance(rectPoints[b].CenterPoints, rectPoints[c].CenterPoints);
    //var ac = Point.Distance(rectPoints[c].CenterPoints, rectPoints[a].CenterPoints);

    //外扩长度偏移量
    int offlength = max / 2;
   
    Point topPoint = new Point();
    Point2f QRMatCenterPoint = new Point();

    //顶点位置
    int selecttop = 0;
    if (max == AB)
    {
        topPoint = rectPoints[c].CenterPoints;//二维码直角顶点
        selecttop = 2;
        //计算二维码的中心坐标
        int X = (rectPoints[a].CenterPoints.X + rectPoints[b].CenterPoints.X) / 2;
        int Y = (rectPoints[a].CenterPoints.Y + rectPoints[b].CenterPoints.Y) / 2;
        QRMatCenterPoint.X = X;
        QRMatCenterPoint.Y = Y;
    }
    else if (max == BC)
    {
        selecttop = 0;
        topPoint = rectPoints[a].CenterPoints;
        int X = (rectPoints[b].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
        int Y = (rectPoints[b].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;
        QRMatCenterPoint.X = X;
        QRMatCenterPoint.Y = Y;
    }
    else if (max == AC)
    {
        selecttop = 1;
        topPoint = rectPoints[b].CenterPoints;
        int X = (rectPoints[a].CenterPoints.X + rectPoints[c].CenterPoints.X) / 2;
        int Y = (rectPoints[a].CenterPoints.Y + rectPoints[c].CenterPoints.Y) / 2;

        QRMatCenterPoint.X = X;
        QRMatCenterPoint.Y = Y;    
    }
    int RotationAngle = -1;

    Point[] QRrect = new Point[4];

    //根号二维码顶点所在象限，计算二维码矩形的四个点
    Point markPosotion = new Point();
    if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
    {
        #region 第一象限
        float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
        float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
        float x1 = QRMatCenterPoint.X - offx;
        float y1 = QRMatCenterPoint.Y - offy;
        QRrect[0] = new Point(x1, y1);
        Math.Max(x1, y1);
        float x2 = (QRMatCenterPoint.X + offx) > 0 ? QRMatCenterPoint.X + offx : 0;
        float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
        QRrect[2] = new Point(x2, y2);
        for (int i = 0; i < rectPoints.Count; i++)
        {
            if (i != selecttop)
            {
                var marks = rectPoints[i].CenterPoints;
                float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                if (marks.Y < QRMatCenterPoint.Y && marks.X < QRMatCenterPoint.X)
                {
                    float x3 = (QRMatCenterPoint.X - voffx);
                    float y3 = (QRMatCenterPoint.Y + voffy);
                    QRrect[1] = new Point(x3, y3);
                }
                else
                {
                    float x4 = (QRMatCenterPoint.X + voffx);
                    float y4 = (QRMatCenterPoint.Y - voffy);
                    QRrect[3] = new Point(x4, y4);
                }

            }
        }
        #endregion
        RotationAngle = 0;
    }
    else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y > topPoint.Y)
    {
        #region 第二象限
        float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
        float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
        float x1 = QRMatCenterPoint.X + offx;
        float y1 = QRMatCenterPoint.Y + offy;
        QRrect[0] = new Point(x1, y1);
        Math.Max(x1, y1);
        float x2 = (QRMatCenterPoint.X - offx) > 0 ? QRMatCenterPoint.X - offx : 0;
        float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
        QRrect[2] = new Point(x2, y2);

        for (int i = 0; i < rectPoints.Count; i++)
        {
            if (i != selecttop)
            {
                var marks = rectPoints[i].CenterPoints;
                float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;
               
                if (marks.Y < QRMatCenterPoint.Y && marks.X < QRMatCenterPoint.X)
                {
                    float x3 = (QRMatCenterPoint.X - voffx);
                    float y3 = (QRMatCenterPoint.Y - voffy);
                    QRrect[1] = new Point(x3, y3);
                }
                else
                {
                    float x4 = (QRMatCenterPoint.X + voffx);
                    float y4 = (QRMatCenterPoint.Y + voffy);
                    QRrect[3] = new Point(x4, y4);
                }
         
            }
        }
        #endregion

        RotationAngle = 90;
    }
    else if (QRMatCenterPoint.X < topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
    {
        #region 第三象限
        //int offset = (int)Distance(QRMatCenterPoint, rectPoints[selecttop].CenterPoints);
        float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
        float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
        float x1 = QRMatCenterPoint.X + offx;
        float y1 = QRMatCenterPoint.Y + offy; 
        QRrect[0] = new Point(x1, y1);
        Math.Max(x1, y1);
        float x2 = (QRMatCenterPoint.X - offx)>0? QRMatCenterPoint.X - offx:0;
        float y2 = (QRMatCenterPoint.Y - offy)>0? QRMatCenterPoint.Y - offy:0;
        QRrect[2] = new Point(x2, y2);

        for (int i = 0; i < rectPoints.Count; i++)
        {
            if (i != selecttop)
            {
                var marks = rectPoints[i].CenterPoints;
                float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                if (marks.Y < QRMatCenterPoint.Y && marks.X > QRMatCenterPoint.X)
                {
                    float x3 = (QRMatCenterPoint.X + voffx);
                    float y3 = (QRMatCenterPoint.Y - voffy);
                    QRrect[1] = new Point(x3, y3);
                }
                else
                {
                    float x4 = (QRMatCenterPoint.X - voffx);
                    float y4 = (QRMatCenterPoint.Y + voffy);
                    QRrect[3] = new Point(x4, y4);
                }

            }
        }
        #endregion

        RotationAngle = 180;
    }
    else if (QRMatCenterPoint.X > topPoint.X && QRMatCenterPoint.Y < topPoint.Y)
    {
        #region 第四象限
        float offx = Math.Abs(rectPoints[selecttop].CenterPoints.X - QRMatCenterPoint.X) * roiscale;
        float offy = Math.Abs(rectPoints[selecttop].CenterPoints.Y - QRMatCenterPoint.Y) * roiscale;
        float x1 = QRMatCenterPoint.X - offx;
        float y1 = QRMatCenterPoint.Y + offy;
        QRrect[0] = new Point(x1, y1);
        Math.Max(x1, y1);
        float x2 = (QRMatCenterPoint.X + offx) > 0 ? QRMatCenterPoint.X + offx : 0;
        float y2 = (QRMatCenterPoint.Y + offy) > 0 ? QRMatCenterPoint.Y + offy : 0;
        QRrect[2] = new Point(x2, y2);


        for (int i = 0; i < rectPoints.Count; i++)
        {
            if (i != selecttop)
            {
                var marks = rectPoints[i].CenterPoints;
                float voffx = Math.Abs(marks.X - QRMatCenterPoint.X) * roiscale;
                float voffy = Math.Abs(marks.Y - QRMatCenterPoint.Y) * roiscale;

                if (marks.Y > QRMatCenterPoint.Y && marks.X > QRMatCenterPoint.X)
                {
                    float x3 = (QRMatCenterPoint.X + voffx);
                    float y3 = (QRMatCenterPoint.Y + voffy);
                    QRrect[1] = new Point(x3, y3);
                }
                else
                {
                    float x4 = (QRMatCenterPoint.X - voffx);
                    float y4 = (QRMatCenterPoint.Y - voffy);
                    QRrect[3] = new Point(x4, y4);
                }
            }
        }
        #endregion
        RotationAngle = -90;
    }
    return QRrect;
}

Mat GetPosotionDetectionPatternsMat(Mat mat, List<RectPoints> rectPoints)
{
    var maxw = rectPoints.Max(w => w.CenterPoints.X);
    var minh = rectPoints.Min(w => w.CenterPoints.Y);
    var prect = rectPoints.FindLast(f => f.CenterPoints.X == maxw);
    ///计算其中一个定位点周长，再根据周长计算单个二维码模块像素大小
    var ArcLength = Cv2.ArcLength(prect.MarkPoints, true);
    var le = (int)(ArcLength / 4);//单边长
    var ModuleSize = (int)(le / 5) + 2;//二维码模块像素大小

    var topx = (int)(maxw - ModuleSize * 5);
    var topy = (int)(minh - ModuleSize * 5);
    topy = topy < 0 ? 1 : topy;//防止越界

    var width = mat.Width - topx - 1;

    var rectwidth = ModuleSize * 11;
    var w = (ModuleSize * 11) > width ? width : (ModuleSize * 10);

    Rect rect = new Rect(topx, topy, w, w);//取得11个模块宽度
    Mat roi = new Mat(mat, rect);
    // Log.Information($"根据二维码模块大小，截取右上角定位点11个模块宽度：宽{roi.Width},高{roi.Height}");
    return roi;
}

Mat HoughLines(Mat mat, int w, string filename)
{
    Mat gray = new Mat();
    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

    Mat mblur = new Mat();
    Cv2.MedianBlur(gray, mblur, 3);
    SaveMatFile(mblur, "MedianBlur", filename);
    // Cv2.GaussianBlur(gray, mblur, new Size(7, 7), 0);

    //SaveMatFile(mblur, "GaussianBlur", filename);

    Mat canny = new Mat();
    // Cv2.Laplacian(mblur, canny, MatType.CV_8U, 5, 1, 0);
    Cv2.Canny(mblur, canny, 100, 200, 3, false);
    //  Cv2.de
    SaveMatFile(canny, "Laplacian", filename);

    Mat tmat = new Mat();
    Cv2.Threshold(mblur, tmat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
    //Cv2.AdaptiveThreshold(mblur, tmat, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 101, 2);
    SaveMatFile(tmat, "AdaptiveThresholdOtsu", filename);

    Mat MorphologyEx_Erode = new Mat();
    Mat elementErode = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(11, 11));
    Cv2.MorphologyEx(tmat, MorphologyEx_Erode, MorphTypes.Erode, elementErode);
    SaveMatFile(MorphologyEx_Erode, "MorphologyEx_Erode", filename);

    Mat MorphologyEx_Dilate = new Mat();
    Mat elementDilate = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(25, 25));
    Cv2.MorphologyEx(MorphologyEx_Erode, MorphologyEx_Dilate, MorphTypes.Dilate, elementErode);
    SaveMatFile(MorphologyEx_Dilate, "MorphologyEx_Dilate", filename);

    Mat BitwiseNot = new Mat();
    Cv2.BitwiseNot(MorphologyEx_Erode, BitwiseNot);
    SaveMatFile(BitwiseNot, "BitwiseNot", filename);

    Point[][] filltercontours;
    HierarchyIndex[] hierarchy;
    ///算出二维码轮廓
    Cv2.FindContours(BitwiseNot, out filltercontours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

    var minArea = (w * w) * 1.5;
    var maxArea = (w * w) * 8.0;
    var minWidthPerimeter = w;
    var maxHeightPerimeter = w * 5;
    Mat draw = new Mat(mat.Size(), mat.Type(), Scalar.All(0));
    Mat drawpoints = new Mat(mat.Size(), mat.Type(), Scalar.All(0));
    Mat drawpointsmat = mat.Clone();
    for (int i = 0; i < filltercontours.Length; i++)
    {
        var area = Cv2.ContourArea(filltercontours[i]);
        if (area >= minArea && area <= maxArea)
        {
            Cv2.DrawContours(mat, filltercontours, i, new Scalar(0, 0, 255), 2, LineTypes.Link8, hierarchy, 0, null);
            SaveMatFile(mat, "matcontours", filename);

            var points = Cv2.ApproxPolyDP(filltercontours[i], Cv2.ArcLength(filltercontours[i], true) * 0.03, true);
            if (points.Length == 4)
            {
                double minCosine = 0;
                double maxCosine = 0;
                CompareDistance(points, out minCosine, out maxCosine);
                if (minCosine / maxCosine >= 0.35 && minCosine >= minWidthPerimeter && maxCosine <= maxHeightPerimeter)
                {
                    for (int j = 0; j < points.Length; j++)
                    {
                        Cv2.Line(drawpointsmat, points[j], points[(j + 1) % 4], Scalar.Red, 3);
                    }
                    SaveMatFile(drawpointsmat, "drawpointsmat", filename);
                }
                else
                {
                    Cv2.DrawContours(drawpoints, filltercontours, i, Scalar.All(255), 3);

                    SaveMatFile(drawpoints, "drawpoints", filename);
                }
            }
        }
        else
        {
            Cv2.DrawContours(draw, filltercontours, i, new Scalar(0, 0, 255), 1, LineTypes.Link8, hierarchy, 0, null);
        }
    }
    SaveMatFile(draw, "draw", filename);

    return mat;
}

void CompareDistance(Point[] points, out double min, out double max)
{
    //double length = 0;
    min = 999999999.0;
    max = -1.0;
    for (int i = 0; i < points.Length; i++)
    {
        var p1 = points[i];
        var p2 = points[(i + 1) % points.Length];
        var dx = p1.DistanceTo(p2);
        min = Math.Min(min, dx);
        max = Math.Max(max, dx);
    }
    // return length;
}
Mat ImgeWarpPerspective(Mat mat,int index)
{

    Mat BGR2GRAY = new Mat();
    Cv2.CvtColor(mat, BGR2GRAY, ColorConversionCodes.BGR2GRAY);
    Mat dst = new Mat();
    Cv2.Threshold(BGR2GRAY, dst, 0,255, ThresholdTypes.Binary| ThresholdTypes.Otsu);

    SaveMatFile(dst, "Threshold", "hsv");

    Mat AdaptiveThreshold = new Mat();
    Cv2.AdaptiveThreshold(BGR2GRAY, AdaptiveThreshold, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 101, 21);
    SaveMatFile(AdaptiveThreshold, "AdaptiveThreshold", "hsv");

    Mat hsv = new Mat(mat.Size(), MatType.CV_8UC3, Scalar.White);
    Cv2.CvtColor(mat, hsv, ColorConversionCodes.BGR2HSV);

    Mat mask1= Mat.Zeros(mat.Size(), mat.Type());
    Mat mask2 = Mat.Zeros(mat.Size(), mat.Type());
    Cv2.InRange(hsv, new Scalar(0, 0, 0), new Scalar(180, 150, 30), mask1);
    Cv2.InRange(hsv, new Scalar(0, 0, 0), new Scalar(180, 255, 30), mask2);


    Mat Erode = new Mat();
    Mat Element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
    Cv2.Dilate(dst, Erode, Element, new Point(-1, -1), 1, BorderTypes.Default, new Scalar(1));
    SaveMatFile(Erode, "ThresholdDilate", "hsv");

    List<RectPoints> channels = GetPatternsPoints(Erode, "hsv");

    Point[] point2F = GetRoiPoint(channels,2.5f);

    for (int i = 0; i < point2F.Length; i++)
    {
        //Cv2.Circle(mat, point2F[i], 3, Scalar.Red, -1);
        mat.Line(point2F[i], point2F[(i + 1) % point2F.Length], Scalar.Red, 3);
        mat.PutText(i.ToString(), point2F[i], HersheyFonts.HersheySimplex, 1.5, Scalar.Blue, 2);
    }
    SaveMatFile(mat, "ThresholdDilateLine", "hsv");

    Mat maxsize = new Mat(mat.Rows*2,mat.Cols, MatType.CV_8UC3, Scalar.White);

    SaveMatFile(mask1, "mask1", "hsv");
    Cv2.BitwiseNot(mask1, mask1);
    // maxsize.CopyTo(backmat);
    //SaveMatFile(mat, "mat", "hsv");
    SaveMatFile(mask1, "mask1BitwiseNot", "hsv");
    SaveMatFile(mask2, "mask2", "hsv");

    //List<Mat> mats = new List<Mat>();

    //mats.Add(mat);
    //mats.Add(backmat);
    Mat result = new Mat();
    Cv2.CvtColor(mask1, result, ColorConversionCodes.BayerBG2RGB);
    // vconcat(vImgs, result); //垂直方向拼接
    Cv2.HConcat(mat, result, maxsize);
   

    //SaveMatFile(maxsize, Guid.NewGuid().ToString(),"hsv",i);
    return mat;
}