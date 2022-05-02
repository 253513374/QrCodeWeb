// See https://aka.ms/new-console-template for more information
using OpenCvSharp;
using System.Collections.Generic;

Console.WriteLine("Hello, World!");
int k = 0;
List<string> files = new List<string>();

var path =  Console.ReadLine();

GetFiles(@"E:\ProjectCode\QrCodeWeb\img", ref files);

int OK = 0;


for (int i = 0; i < files.Count; i++)
{
    DateTime date = DateTime.Now;
    var file = files[i];
    var filename = Path.GetFileName(file).Replace(".jpg", "");
    using Mat Src = Cv2.ImRead(file);
    Mat GRAY_mat ;
    SaveMatFile(Src, $"原图", filename);
    //Cv2.CvtColor(Src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图
    //SaveMatFile(GRAY_mat, $"GRAY_mat", filename);
    
    var qrcode =   Decode(Src, filename, out GRAY_mat);

    if (GRAY_mat is not null)
    {
        Mat w = WarpAffine(GRAY_mat, filename);


        List<RectPoints> rectPoints = await MatPreprocessing(w.Clone(), filename);

        //if (rectPoints.Count == 3)
        //{
        //    OK++;
        //}

        if (rectPoints.Count >= 3)
        {
            OK++;
        }
        Console.WriteLine($"{i+1}:{Difference(date)}:完成{filename}：{qrcode},找到定点:{rectPoints.Count};");
    }
    else
    {
        Console.WriteLine($"{i+1}:{Difference(date)}：完成{filename}：{qrcode}-- 找不到二维码");
    }
    k = 0;
}

var b = Math.Round((double)OK / (double)files.Count, 3) * 100;
Console.WriteLine($"共：{files.Count}；成功数量：{OK};成功率：{b}%");


async Task<List<RectPoints>> MatPreprocessing(Mat src,string filename)
{

    Mat GRAY_mat = src.Clone();
    // Cv2.CvtColor(src, GRAY_mat, ColorConversionCodes.BGR2GRAY);//转成灰度图

    // using Mat ScaleAbs_mat = new Mat();
    // GRAY_mat.ConvertTo(GRAY_mat, MatType.CV_8UC1, 2, 7);
    //using Mat mat = new Mat();
    //InputArray kernel2 = InputArray.Create<int>(new int[3, 3] { { 0, -1, 0 }, { -1, 5, -1 }, { 0, -1, 0 } });
    //using Mat filter2Dmat = new Mat();
    //Cv2.Filter2D(GRAY_mat, filter2Dmat, MatType.CV_8UC1, kernel2, anchor: new Point(1, 1), delta: 20, borderType: BorderTypes.Constant);
    //using Mat MedianBlur = new Mat();
    //Cv2.MedianBlur(filter2Dmat, MedianBlur, 11);
    //SaveMatFile(MedianBlur, $"MedianBlur", filename);


    //Mat DST = new Mat();
    //Cv2.Normalize(MedianBlur, DST, 0, 255, NormTypes.MinMax, 1);
    //Mat Threshold_mat = new Mat();
    //  Cv2.Threshold(MedianBlur, Threshold_mat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
    // SaveMatFile(Threshold_mat, $"Threshold_Binary", filename);
    //Cv2.AdaptiveThreshold(MedianBlur, Threshold_mat, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 101, 2);
    //Cv2.Threshold(MedianBlur, Threshold_mat, 0,255, ThresholdTypes.Binary|ThresholdTypes.Otsu);


    // SaveMatFile(Threshold_mat, $"AdaptiveThreshold", filename);

    //Mat elementDilate = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
    //Mat Dilate = new Mat();
    //Cv2.Dilate(GRAY_mat, Dilate, elementDilate);
    //SaveMatFile(Dilate, $"Dilate", filename);


    //Mat elementErode = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
    //Mat Erode = new Mat();
    //Cv2.Erode(GRAY_mat, Erode, elementErode);

    int moduleSzie = (int)((GRAY_mat.Width / 31)/2);

    Mat MorphologyEx_Open = new Mat();
    Mat elementOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
    Cv2.MorphologyEx(GRAY_mat, MorphologyEx_Open, MorphTypes.Dilate, elementOpen);
    SaveMatFile(MorphologyEx_Open, "MorphologyEx_Open", filename);

    Mat MorphologyEx_Close = new Mat();
    Mat elementClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
    Cv2.MorphologyEx(MorphologyEx_Open, MorphologyEx_Close, MorphTypes.Erode, elementClose);
    SaveMatFile(MorphologyEx_Close, "MorphologyEx_Close", filename);

    //Mat Gradient = new Mat();
    //Mat elementGradient = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(5, 5));
    //Cv2.MorphologyEx(MorphologyEx_Close, Gradient, MorphTypes.Gradient, elementClose);
    //SaveMatFile(Gradient, "elementGradient", filename);
    List<RectPoints> rectPoints = GetPosotionDetectionPatternsPoints(MorphologyEx_Close, filename);

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

Mat WarpAffine(Mat roi,string filename)
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
    Cv2.Threshold(Erode, Threshold_mat, 0,255,  ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
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

    using Mat MedianBlur = new Mat();
    Cv2.MedianBlur(GRAY_mat, MedianBlur, 11);
    SaveMatFile(MedianBlur, "MedianBlur", filename);

    Mat srthreshold = new Mat();
    //  Cv2.Threshold(MedianBlur, Threshold_mat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
    // SaveMatFile(Threshold_mat, $"Threshold_Binary", filename);
    Cv2.AdaptiveThreshold(MedianBlur, srthreshold, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 101, 2);

    SaveMatFile(srthreshold, "圈定的二维码边界二值化", filename);

    
    RotatedRect rotated = Cv2.MinAreaRect(tp.ToArray());
    Point2f[] srcpoint = new Point2f[]
    {
        new Point2f(tp[0].X,tp[0].Y),
        new Point2f(tp[1].X,tp[1].Y),
        new Point2f(tp[2].X,tp[2].Y),
        new Point2f(tp[3].X,tp[3].Y)
    };
    //定义变换之后的二维码Size
    int sizew = Math.Max((int)rotated.Size.Width+50, (int)rotated.Size.Height+50);
    int boxw = 30;
    Rect rect = new Rect(0, 0, sizew, sizew);
    Point2f[] dstpoint = new Point2f[4];
    dstpoint[0] = new Point2f(boxw, boxw);
    dstpoint[1] = new Point2f(boxw, sizew- boxw);
    dstpoint[2] = new Point2f(sizew- boxw, sizew- boxw);
    dstpoint[3] = new Point2f(sizew- boxw, boxw);

    //对二维码区域进行透视变换，
    using Mat warpMatrix = Cv2.GetPerspectiveTransform(srcpoint, dstpoint);
    Mat dst = new Mat(rect.Size, MatType.CV_8UC3);
    Cv2.WarpPerspective(srthreshold, dst, warpMatrix, dst.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

   // dst.ConvertTo(dst, MatType.CV_8UC1, 2, 10);
    SaveMatFile(dst, "透视变换结果", filename);

    //var arearect = Cv2.MinAreaRect(pts.ToArray());
    //var center = arearect.Center;
    //var angle = arearect.Angle;
    //var M = Cv2.GetRotationMatrix2D(center, angle, 1.0);
    //Mat mdst = new Mat();
    //Cv2.WarpAffine(roi, mdst, M, roi.Size());
   
    return dst;
}

Task SaveMatFile(Mat array, string name,string FileNmae,int i=0)
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
    k ++;
    //ImageEncodingParam param = new ImageEncodingParam(ImageEncodingFlags.);
    array.SaveImage(filepath);
    return Task.CompletedTask;
}

List<RectPoints> GetPosotionDetectionPatternsPoints(Mat dilatemat,string name)
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
        // 轮廓圈套层数
        rectPoints = new List<RectPoints>();

        double moduleSzie = dilatemat.Width / 31;
        double minAreaSzie = (moduleSzie* 4)* (moduleSzie *4);
        double maxAreaSzie = (moduleSzie * 10) * (moduleSzie * 10);
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
                Cv2.DrawContours(drawingAllContours, matcontours, i, new Scalar(255, 255, 255));
                SaveMatFile(drawingAllContours, "所有轮廓", name);

                // 保存找到的三个黑色定位角
                var points2 = Cv2.ApproxPolyDP(matcontours[i], 0.03 * Cv2.ArcLength(matcontours[i].ToArray(), true), true);// 

                if (points2.Length == 4)
                {
                    if(name== "1650182312378")
                    {
                        int s = 0;
                    }
                    var  area = (int)Cv2.ContourArea(matcontours[i], false);
                    if (area > minAreaSzie && area<maxAreaSzie)
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
                    }
                    else
                    {
                        Cv2.DrawContours(drawingmarkminAreaSzie, matcontours, i, new Scalar(0, 125, 255), 4, LineTypes.Link8);
                        SaveMatFile(drawingmarkminAreaSzie, "三个定位点轮廓_小于指定面积", name);
                    }
                }
                ic = 0;
                parentIdx = -1;

            }

        }
     
      
     

        return rectPoints;
    }
    catch (Exception ee)
    {
        return new List<RectPoints>();
       // throw;
    }
  
}


string Decode(Mat src,string filename, out Mat dst)
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

   

    RotatedRect rotatedRect = ss.MinAreaRect();
    var center = rotatedRect.Center;
    var size2F = rotatedRect.Size;
    var MAX = Math.Max(size2F.Width, size2F.Height);
    var RoiSize = new Size(MAX + (MAX / 2), MAX + (MAX / 2));
    var RoiPoint = new Point(center.X - (MAX / 2) - (MAX / 4), center.Y - (MAX / 2) - (MAX / 4));

    Rect rectroi = new Rect(RoiPoint, RoiSize);
    dst = new Mat(src, rectroi);

    SaveMatFile(dst, "二维码截取", filename);
    //   warpAffine(dst, image, M, sz);
    return texts[0];
}
