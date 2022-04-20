using OpenCvSharp;

namespace QrCodeWeb.Datas
{
    public class RectPoints
    {

        //public RectPoints()
        //{
        //    this.Points = new List<Point>();
        //    this.MarkPoints = new List<Point[]>();
        //}
        public Point CenterPoints { set; get; } 

        public Point[] MarkPoints { set; get; }
    }
}
