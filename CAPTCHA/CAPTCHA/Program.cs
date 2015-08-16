using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Preprocess;

namespace CAPTCHA
{
    class Program
    {
        public static int Height = 0;
        public static int Width = 0;

        static void Main(string[] args)
        {
            Bitmap bmp = new Bitmap(@"D:\CSharp\CAPTCHA\Orgin\img3.jpg");

            Height = bmp.Height;

            Width = bmp.Width; 

            Bitmap segBmp = CaptchaSegment.CaptchaSegment.CaptchaSegmentFun(bmp);

            segBmp.Save(@"D:\CSharp\CAPTCHA\DST\b3.bmp", System.Drawing.Imaging.ImageFormat.Jpeg);
        }
    }
}
