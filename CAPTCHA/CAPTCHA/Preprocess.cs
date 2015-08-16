/* --------------------------------------------------------
 * 作者：livezingy
 * 
 * 博客：http://www.livezingy.com
 * 
 * 开发环境：
 *      Visual Studio V2012
 *      .NET Framework 4.5
 *      
 * 版本历史：
 *      V1.0    2015年07月14日
                获取验证码二值化数组，灰度数组；有二值化数组，灰度数组等转换为位图         
--------------------------------------------------------- */

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

using System.Runtime.InteropServices;

using CaptchaSegment;


namespace Preprocess
{
    public class VerPro
    {
        //字符在参考线以上区域的投影区域集合
        private ArrayList _vRange;

        public ArrayList vRange
        {
            get { return _vRange; }
            set { _vRange = value; }
        }

        //字符与参考线的交点
        private Point _vInter;

        public Point vInter
        {
            get { return _vInter; }
            set { _vInter = value; }
        }
    }

    public static class Preprocess
    {
        /// <summary>
        /// 在二值化数组中寻找与给定坐标点连通的区域，并用涂上灰色
        /// </summary>
        /// <param name="nRow">给定坐标行</param>
        /// <param name="nCol">给定坐标列</param>
        /// <param name="imageHeight">二值化图像的高度</param>
        /// <param name="imageWidth">二值化图像的宽度</param>
        /// <param name="BinaryArray">二值化数组</param>
        /// <returns>返回与给定坐标点连通区域设定为灰色的灰度数组</returns>
        public static Byte[,] FillConnectedArea(int nRow, int nCol, int imageHeight, int imageWidth, Byte[,] BinaryArray)
        {
            for (int x = nRow - 1;x < nRow + 2;x ++)
            {
                for (int y = nCol - 1;y < nCol + 2;y ++)
                {
                    if ((x >= 0) && (x < imageHeight) && (y >= 0) && (y < imageWidth))
                    {
                       // if (255 == BinaryArray[x, y])//8邻域的方式寻找连通域
                        if ((255 == BinaryArray[x, y]) && ((x == nRow) || (y == nCol)))//4邻域的方式寻找连通域
                        {
                            BinaryArray[x, y] = 128;

                            FillConnectedArea(x, y, imageHeight, imageWidth, BinaryArray);
                        }
                    }
                }
            }

            return BinaryArray;
        }

        /// <summary>
        ///获取指定区域下黑色像素的竖直投影
        /// </summary>
        /// <param name="leftdown">待处理区域的左下角坐标</param>
        /// <param name="rightUp">待处理区域的右上角坐标</param>
        /// <param name="flg">参考线，1：baseline为参考线，2:meanline为参考线,0：无参考线</param>
        /// <param name="srcArray">待处理的二值化数组</param>
        /// <returns>返回指定区域投影信息</returns>
        public static VerPro VerticalProjection(Point leftdown, Point rightUp, byte flg, Byte[,] srcArray)
        {
            int tmpNum = CAPTCHA.Program.Width;

            Int32[] vProjection = new Int32[tmpNum];

            Array.Clear(vProjection, 0, tmpNum);

            Point guideP = CaptchaSegment.CaptchaSegment.GuidePoint;

            //记录当前投影区域的区块数目，相关信息在区块中以Point的形式存在,Point.x为区块数目，Point.y为该区块的起始位置
            ArrayList vpArea = new ArrayList();

            VerPro infoPro = new VerPro();

            Point vPoint = new Point();
            Point interPoint = new Point();

            int x = 0;
            int y = 0;

            //获取指定区域的竖直投影
            for (y = leftdown.Y; y < rightUp.Y; y++)
            {
                if(2 == flg)
                {
                    x = leftdown.X;
                }
                else
                {
                    x = leftdown.X + 1;
                }
                for (x = leftdown.X; x < rightUp.X; x++)
                {
                    if (0 == srcArray[x, y])
                    {
                        vProjection[y]++;
                    }
                }
            }

            //获取竖直投影的区块
            int k = leftdown.Y;

            while (k < rightUp.Y)
            {
                int tmpMax = 0;

                if (vProjection[k] != 0)
                {
                    vPoint.X = k - 1;

                    while ((vProjection[k] != 0) && (k < rightUp.Y))
                    {
                        k ++;
                    }

                    vPoint.Y = k;

                    if(2 == flg)
                    {
                        tmpMax = rightUp.X;
                        //若meanline为基准线，则找到当前区域字符像素X值最小的点
                        for (y = vPoint.X; y < vPoint.Y; y++)                
                        {
                            for (x = leftdown.X; x < rightUp.X; x++)
                            {
                                if ((0 == srcArray[x, y]) && (tmpMax > x))
                                {
                                    tmpMax = x;
                                    break;
                                }
                            }
                        }
                    }
                    else if(1 == flg)
                    {
                        tmpMax = leftdown.X;
                        //若baseline为基准线，则找到当前区域字符像素X值最大的点
                        for (y = vPoint.X; y < vPoint.Y; y++)
                        {
                            for (x = rightUp.X; x > leftdown.X; x--)
                            {
                                if ((0 == srcArray[x, y]) && (tmpMax < x))
                                {
                                    tmpMax = x;
                                    break;
                                }
                            }
                        }
                    }

                    //在meanline或baseline之外2个像素的区域被记录，否则被认定为无效区域
                    if ((((tmpMax - guideP.X) > 1) && (1 == flg)) || (((guideP.Y - tmpMax) > 2)) && (2 == flg) || (0 == flg))
                    {
                        vpArea.Add(vPoint);
                    }
                }
                k ++;
            }

            if ((flg != 0) && (vpArea.Count > 0))
            {
                if (2 == flg)
                {
                    tmpNum = guideP.Y;
                }
                else
                {
                    tmpNum = guideP.X;
                }

                //从第一个有效投影区域开始从左至右找字符与参考线的左侧交点
                vPoint = (Point)vpArea[0];
                y = vPoint.X;
                while (y < vPoint.Y)
                {
                    if (0 == srcArray[tmpNum,y])
                    {
                        interPoint.X = y;
                        break;
                    }
                    y++;
                }


                //从最后一个有效投影区域开始从右至左找字符与参考线的右侧交点
                vPoint = (Point)vpArea[vpArea.Count - 1];
                y = vPoint.Y;
                while (y > vPoint.X)
                {
                    if (0 == srcArray[tmpNum, y])
                    {
                        interPoint.Y = y;
                        break;
                    }
                    y--;
                }
            }

            infoPro.vRange = vpArea;
            infoPro.vInter = interPoint;
            return infoPro;
        }


        /// <summary>
        ///获取字符所在区域的边界
        /// </summary>
        /// <param name="imageHeight">二值化图像的高度</param>
        /// <param name="imageWidth">二值化图像的宽度</param>
        /// <param name="BinaryArray">二值化数组</param>
        /// <returns>返回字符所在区域的边界</returns>
        public static Boundary getImgBoundary(int imageHeight, int imageWidth, Byte[,] BinaryArray)
        {
            Boundary boundary = new Boundary();

            Point leftDownP = new Point();
            Point rightUpP = new Point();

            Int32[] verticalPoints = new Int32[imageWidth];
            Array.Clear(verticalPoints, 0, imageWidth);

            int x = 0, y = 0;

            for (x = 0; x < imageHeight; x++)
            {
                for (y = 0; y < imageWidth; y++)
                {
                    if (0 == BinaryArray[x, y])
                    {
                        verticalPoints[y]++;
                    }
                }
            }

            //用于存储当前横坐标水平方向上的有效像素点数量(组成字符的像素点)
            Int32[] horPoints = new Int32[imageHeight];
            Array.Clear(horPoints, 0, imageHeight);

            //统计源图像的二值化数组中在每一个横坐标的水平方向所包含的像素点数
            
             for (y = 0; y < imageWidth; y++)
            {
                for (x = 0; x < imageHeight; x++)
                {
                    if (0 == BinaryArray[x, y])
                    {
                        horPoints[x]++;
                    }
                }
            }

            //从原点开始，在竖直投影中找到左下角的Y坐标
            for(y = 0; y < imageWidth; y++)
            {
                if(verticalPoints[y] != 0)
                {
                    if(y != 0)
                    {
                        leftDownP.Y = y - 1;
                    }
                    else
                    {
                        leftDownP.Y = y;
                    }
                    break;
                }
            }

            //从离原点最远的点开始，在竖直投影中找到右上角的Y坐标
            for(y = imageWidth - 1; y > 0; y--)
            {
                if(verticalPoints[y] != 0)
                {
                    if (y != imageWidth - 1)
                    {
                        rightUpP.Y = y + 1;
                    }
                    else
                    {
                        rightUpP.Y = y;
                    }
                    break;
                }
            }

            //从原点开始，在竖直投影中找到左下角的X坐标
            for (x = 0; x < imageHeight; x++)
            {
                if (horPoints[x] != 0)
                {
                    if(x != 0)
                    {
                        leftDownP.X = x - 1;
                    }
                    else
                    {
                        leftDownP.X = x;
                    }
                    break;
                }
            }

            //从离原点最远的点开始，在竖直投影中找到右上角的X坐标
            for (x = imageHeight - 1; x > 0; x--)
            {
                if (horPoints[x] != 0)
                {
                    if (x != imageHeight - 1)
                    {
                        rightUpP.X = x + 1; 
                    }
                    else
                    {
                        rightUpP.X = x;
                    }
                    break;
                }
            }

            boundary.leftDownPoint = leftDownP;
            boundary.rightUpPoint = rightUpP;

            return boundary;
        }

        /// <summary>
        /// 获取验证码字符的笔画宽度。横向与纵向分别统计黑色像素的宽度，默认在这两个方向上最多的宽度值为字符笔画宽度。
        /// 该方法默认有效字符为黑色，不适用于统计倾斜严重的验证码，
        /// </summary>
        /// <param name="imageHeight">二值化图像的高度</param>
        /// <param name="imageWidth">二值化图像的宽度</param>
        /// <param name="BinaryArray">二值化数组</param>
        /// <returns>返回字符所在区域的边界</returns>
        public static Int32 GetStrokeWid(int imageHeight, int imageWidth, Byte[,] BinaryArray)
        {
            int i, j;
            int nCount = 0;
            ArrayList nCountArray = new ArrayList();

            //从宽度方向上统计黑色像素的宽度
            for (i = 0; i < imageHeight; i++)
            {
                for (j = 0; j < imageWidth; j++)
                {
                    if (0 == BinaryArray[i, j])
                    {
                        nCount++;
                    }
                    else
                    {
                        if (nCount != 0)
                        {
                            nCountArray.Add(nCount);
                        }

                        nCount = 0;
                    }
                }
            }

            //从高度方向上统计黑色像素的宽度
            for (j = 0; j < imageWidth; j++)
            {
                for (i = 0; i < imageHeight; i++)
                {
                    if (0 == BinaryArray[i, j])
                    {
                        nCount++;
                    }
                    else
                    {
                        if (nCount != 0)
                        {
                            nCountArray.Add(nCount);
                        }

                        nCount = 0;
                    }
                }
            }
            //将nCountArray中的数值按照从小到大的顺序进行排列
            nCountArray.Sort();

            int tmpNum = nCountArray.Count;

            tmpNum = (int)nCountArray[tmpNum - 1] + 1;

            //统计宽度数最多的值
            int[] Histogram = new int[tmpNum];
            Array.Clear(Histogram, 0, tmpNum);
            foreach (int val in nCountArray)
            {
                Histogram[val]++;
            }

            tmpNum = Histogram.Max();
            int StrokeWid = 0;
            
            for (i = 0; i < 2; i ++ )
            {
                tmpNum = Histogram.Max();

                j = Array.IndexOf(Histogram, tmpNum);

                Histogram.SetValue(0, j);

                StrokeWid = StrokeWid + j;
            }

            return (int)Math.Round((Double)StrokeWid / 2);
            
        }
    
        /// <summary>
        /// 实现Sauvola算法实现图像二值化
        /// </summary>
        /// <param name="bin_image">用于存储二值化完成的图像</param>
        /// <param name="gray_image">用于存储等待二值化完成的灰度图像</param>
        public static Byte[,] Sauvola(Byte[,] gray_image)
        {
            //以当前像素点为中心的邻域的宽度
            int w = 40;

            //使用者自定义的修正系数
            double k = 0.3;

            //邻域边界距离中心点的距离
            int  whalf = w >> 1;
            int MAXVAL = 256;     

            int image_width = gray_image.GetLength(0);
            int image_height = gray_image.GetLength(1);

            Byte[,] bin_image = new Byte[image_width, image_height];
          

            int[,] integral_image = new int[image_width, image_height];
            int[,] integral_sqimg = new int[image_width, image_height];
            int[,] rowsum_image = new int[image_width, image_height];
            int[,] rowsum_sqimg = new int[image_width, image_height];

           
            int xmin,ymin,xmax,ymax;
            double diagsum,idiagsum,diff,sqdiagsum,sqidiagsum,sqdiff,area;
            double mean,std,threshold;

            for (int j = 0; j < image_height; j++)
            {
                rowsum_image[0, j] = gray_image[0, j];
                rowsum_sqimg[0, j] = gray_image[0, j] * gray_image[0, j];
            }
            for (int i = 1; i < image_width; i++)
            {
                for (int j = 0; j < image_height; j++)
                {
                    //计算图像范围内任意宽度窗口(邻域)的灰度值之和
                    rowsum_image[i, j] = rowsum_image[i - 1, j] + gray_image[i, j];

                    //计算图像范围内任意宽度窗口(邻域)的灰度值平方之和
                    rowsum_sqimg[i, j] = rowsum_sqimg[i - 1, j] + gray_image[i, j] * gray_image[i, j];
                }
            }

            for (int i = 0; i < image_width; i++)
            {
                integral_image[i, 0] = rowsum_image[i, 0];
                integral_sqimg[i, 0] = rowsum_sqimg[i, 0];
            }
            for (int i = 0; i < image_width; i++)
            {
                for (int j = 1; j < image_height; j++)
                {
                    //计算图像范围内任意宽度窗口(邻域)的灰度值的积分
                    integral_image[i, j] = integral_image[i, j - 1] + rowsum_image[i, j];

                    //计算图像范围内任意宽度窗口(邻域)的灰度值平方的积分
                    integral_sqimg[i, j] = integral_sqimg[i, j - 1] + rowsum_sqimg[i, j];
                }
            }

            //Calculate the mean and standard deviation using the integral image

            for(int i=0; i<image_width; i++){
                for(int j=0; j<image_height; j++){
                    xmin = Math.Max(0,i-whalf);
                    ymin = Math.Max(0, j - whalf);
                    xmax = Math.Min(image_width - 1, i + whalf);
                    ymax = Math.Min(image_height - 1, j + whalf);
                    area = (xmax-xmin+1)*(ymax-ymin+1);
                    // area can't be 0 here
                    // proof (assuming whalf >= 0):
                    // we'll prove that (xmax-xmin+1) > 0,
                    // (ymax-ymin+1) is analogous
                    // It's the same as to prove: xmax >= xmin
                    // image_width - 1 >= 0         since image_width > i >= 0
                    // i + whalf >= 0               since i >= 0, whalf >= 0
                    // i + whalf >= i - whalf       since whalf >= 0
                    // image_width - 1 >= i - whalf since image_width > i
                    // --IM
                    if (area <= 0)
                        throw new Exception("Binarize: area can't be 0 here");
                    if (xmin == 0 && ymin == 0)
                    { // Point at origin
                        diff = integral_image[xmax, ymax];
                        sqdiff = integral_sqimg[xmax, ymax];
                    }
                    else if (xmin == 0 && ymin > 0)
                    { // first column
                        diff = integral_image[xmax, ymax] - integral_image[xmax, ymin - 1];
                        sqdiff = integral_sqimg[xmax, ymax] - integral_sqimg[xmax, ymin - 1];
                    }
                    else if (xmin > 0 && ymin == 0)
                    { // first row
                        diff = integral_image[xmax, ymax] - integral_image[xmin - 1, ymax];
                        sqdiff = integral_sqimg[xmax, ymax] - integral_sqimg[xmin - 1, ymax];
                    }
                    else
                    { // rest of the image
                        diagsum = integral_image[xmax, ymax] + integral_image[xmin - 1, ymin - 1];
                        idiagsum = integral_image[xmax, ymin - 1] + integral_image[xmin - 1, ymax];
                        //以(i,j)为中心点的w邻域内灰度值的积分
                        diff = diagsum - idiagsum;

                        sqdiagsum = integral_sqimg[xmax, ymax] + integral_sqimg[xmin - 1, ymin - 1];
                        sqidiagsum = integral_sqimg[xmax, ymin - 1] + integral_sqimg[xmin - 1, ymax];
                        //以(i,j)为中心点的w邻域内灰度值平方的积分
                        sqdiff = sqdiagsum - sqidiagsum;
                    }

                    //以(i,j)为中心点的w邻域内的灰度均值
                    mean = diff/area;

                    //以(i,j)为中心点的w邻域内的标准方差
                    std  = Math.Sqrt((sqdiff - diff*diff/area)/(area-1));

                    //根据Sauvola计算公式和以(i,j)为中心点的w邻域内的灰度均值与标准方差来计算当前点(i,j)的二值化阈值
                    threshold = mean*(1+k*((std/128)-1));

                    //根据当前点的阈值对当前像素点进行二值化
                    if(gray_image[i,j] < threshold)
                        bin_image[i,j] = 0;
                    else
                        bin_image[i,j] = (byte)(MAXVAL-1);
                }
            }

            return bin_image;
        }
        
        /// <summary>
        /// 图像二值化方法：大津法和迭代法
        /// </summary>
        public enum BinarizationMethods
        {
            Otsu,       // 大津法
            Iterative   // 迭代法
        }

        /// <summary>
        /// 全局阈值图像二值化
        /// </summary>
        /// <param name="bmp">原始图像</param>
        /// <param name="method">二值化方法</param>
        /// <param name="threshold">输出：全局阈值</param>
        /// <returns>二值化图像</returns>
        public static Bitmap ToBinaryBitmap(this Bitmap bmp, BinarizationMethods method, out Int32 threshold)
        {   // 位图转换为灰度数组
            Byte[,] GrayArray = bmp.ToGrayArray();

            // 计算全局阈值
            if (method == BinarizationMethods.Otsu)
                threshold = OtsuThreshold(GrayArray);
            else
                threshold = IterativeThreshold(GrayArray);

            // 将灰度数组转换为二值数据
            Int32 PixelHeight = bmp.Height;
            Int32 PixelWidth = bmp.Width;
            Int32 Stride = ((PixelWidth + 31) >> 5) << 2;
            Byte[] Pixels = new Byte[PixelHeight * Stride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Base = i * Stride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    if (GrayArray[i, j] > threshold)
                    {
                        Pixels[Base + (j >> 3)] |= Convert.ToByte(0x80 >> (j & 0x7));
                    }
                }
            }

            // 从二值数据中创建黑白图像
            Bitmap BinaryBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format1bppIndexed);

            // 设置调色表
            ColorPalette cp = BinaryBmp.Palette;
            cp.Entries[0] = Color.Black;    // 黑色
            cp.Entries[1] = Color.White;    // 白色
            BinaryBmp.Palette = cp;

            // 设置位图图像特性
            BitmapData BinaryBmpData = BinaryBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
            Marshal.Copy(Pixels, 0, BinaryBmpData.Scan0, Pixels.Length);
            BinaryBmp.UnlockBits(BinaryBmpData);

            return BinaryBmp;
        }

        /// <summary>
        /// 全局阈值图像二值化
        /// </summary>
        /// <param name="bmp">原始图像</param>
        /// <param name="method">二值化方法</param>
        /// <param name="threshold">输出：全局阈值</param>
        /// <returns>二值化后的图像数组</returns>       
        public static Byte[,] ToBinaryArray(this Bitmap bmp, BinarizationMethods method, out Int32 threshold)
        {   // 位图转换为灰度数组
            Byte[,] GrayArray = bmp.ToGrayArray();

            // 计算全局阈值
            if (method == BinarizationMethods.Otsu)
                threshold = OtsuThreshold(GrayArray);
            else
                threshold = IterativeThreshold(GrayArray);

            // 根据阈值进行二值化
            Int32 PixelHeight = bmp.Height;
            Int32 PixelWidth = bmp.Width;
            Byte[,] BinaryArray = new Byte[PixelHeight, PixelWidth];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    BinaryArray[i, j] = Convert.ToByte((GrayArray[i, j] > threshold) ? 255 : 0);
                }
            }

            return BinaryArray;
        }

        /// <summary>
        /// 大津法计算阈值
        /// </summary>
        /// <param name="grayArray">灰度数组</param>
        /// <returns>二值化阈值</returns>
        public static Int32 OtsuThreshold(Byte[,] grayArray)
        {   // 建立统计直方图
            Int32[] Histogram = new Int32[256];
            Array.Clear(Histogram, 0, 256);     // 初始化
            foreach (Byte b in grayArray)
            {
                Histogram[b]++;                 // 统计直方图
            }

            // 总的质量矩和图像点数
            Int32 SumC = grayArray.Length;    // 总的图像点数
            Double SumU = 0;                  // 双精度避免方差运算中数据溢出
            for (Int32 i = 1; i < 256; i++)
            {
                SumU += i * Histogram[i];     // 总的质量矩               
            }

            // 灰度区间
            Int32 MinGrayLevel = Array.FindIndex(Histogram, NonZero);       // 最小灰度值
            Int32 MaxGrayLevel = Array.FindLastIndex(Histogram, NonZero);   // 最大灰度值

            // 计算最大类间方差
            Int32 Threshold = MinGrayLevel;
            Double MaxVariance = 0.0;       // 初始最大方差
            Double U0 = 0;                  // 初始目标质量矩
            Int32 C0 = 0;                   // 初始目标点数
            for (Int32 i = MinGrayLevel; i < MaxGrayLevel; i++)
            {
                if (Histogram[i] == 0) continue;

                // 目标的质量矩和点数               
                U0 += i * Histogram[i];
                C0 += Histogram[i];

                // 计算目标和背景的类间方差
                Double Diference = U0 * SumC - SumU * C0;
                Double Variance = Diference * Diference / C0 / (SumC - C0); // 方差
                if (Variance > MaxVariance)
                {
                    MaxVariance = Variance;
                    Threshold = i;
                }
            }

            // 返回类间方差最大阈值
            return Threshold;
        }

        /// <summary>
        /// 检测非零值
        /// </summary>
        /// <param name="value">要检测的数值</param>
        /// <returns>
        ///     true：非零
        ///     false：零
        /// </returns>
        private static Boolean NonZero(Int32 value)
        {
            return (value != 0) ? true : false;
        }

        /// <summary>
        /// 迭代法计算阈值
        /// </summary>
        /// <param name="grayArray">灰度数组</param>
        /// <returns>二值化阈值</returns>
        public static Int32 IterativeThreshold(Byte[,] grayArray)
        {   // 建立统计直方图
            Int32[] Histogram = new Int32[256];
            Array.Clear(Histogram, 0, 256);     // 初始化
            foreach (Byte b in grayArray)
            {
                Histogram[b]++;                 // 统计直方图
            }

            // 总的质量矩和图像点数
            Int32 SumC = grayArray.Length;    // 总的图像点数
            Int32 SumU = 0;
            for (Int32 i = 1; i < 256; i++)
            {
                SumU += i * Histogram[i];     // 总的质量矩               
            }

            // 确定初始阈值
            Int32 MinGrayLevel = Array.FindIndex(Histogram, NonZero);       // 最小灰度值
            Int32 MaxGrayLevel = Array.FindLastIndex(Histogram, NonZero);   // 最大灰度值
            Int32 T0 = (MinGrayLevel + MaxGrayLevel) >> 1;
            if (MinGrayLevel != MaxGrayLevel)
            {
                for (Int32 Iteration = 0; Iteration < 100; Iteration++)
                {   // 计算目标的质量矩和点数
                    Int32 U0 = 0;
                    Int32 C0 = 0;
                    for (Int32 i = MinGrayLevel; i <= T0; i++)
                    {   // 目标的质量矩和点数               
                        U0 += i * Histogram[i];
                        C0 += Histogram[i];
                    }

                    // 目标的平均灰度值和背景的平均灰度值的中心值
                    Int32 T1 = (U0 / C0 + (SumU - U0) / (SumC - C0)) >> 1;
                    if (T0 == T1) break; else T0 = T1;
                }
            }

            // 返回最佳阈值
            return T0;
        }

        /// <summary>
        /// 将原始图像转换成格式为Bgr565的16位图像
        /// </summary>
        /// <param name="bmp">用于转换的原始图像</param>
        /// <returns>转换后格式为Bgr565的16位图像</returns>
        public static Bitmap ToBgr565(this Bitmap bmp)
        {
            Int32 PixelHeight = bmp.Height; // 图像高度
            Int32 PixelWidth = bmp.Width;   // 图像宽度
            Int32 Stride = ((PixelWidth * 3 + 3) >> 2) << 2;    // 跨距宽度
            Byte[] Pixels = new Byte[PixelHeight * Stride];

            // 锁定位图到系统内存
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(bmpData.Scan0, Pixels, 0, Pixels.Length);  // 从非托管内存拷贝数据到托管内存
            bmp.UnlockBits(bmpData);    // 从系统内存解锁位图

            // Bgr565格式为 RRRRR GGGGGG BBBBB
            Int32 TargetStride = ((PixelWidth + 1) >> 1) << 2;  // 每个像素占2字节，且跨距要求4字节对齐
            Byte[] TargetPixels = new Byte[PixelHeight * TargetStride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Index = i * Stride;
                Int32 Loc = i * TargetStride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    Byte B = Pixels[Index++];
                    Byte G = Pixels[Index++];
                    Byte R = Pixels[Index++];

                    TargetPixels[Loc++] = (Byte)(((G << 3) & 0xe0) | ((B >> 3) & 0x1f));
                    TargetPixels[Loc++] = (Byte)((R & 0xf8) | ((G >> 5) & 7));
                }
            }

            // 创建Bgr565图像
            Bitmap TargetBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format16bppRgb565);

            // 设置位图图像特性
            BitmapData TargetBmpData = TargetBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
            Marshal.Copy(TargetPixels, 0, TargetBmpData.Scan0, TargetPixels.Length);
            TargetBmp.UnlockBits(TargetBmpData);

            return TargetBmp;
        }

        /// <summary>
        /// 将原始图像转换成格式为Bgr555的16位图像
        /// </summary>
        /// <param name="bmp">用于转换的原始图像</param>
        /// <returns>转换后格式为Bgr555的16位图像</returns>
        public static Bitmap ToBgr555(this Bitmap bmp)
        {
            Int32 PixelHeight = bmp.Height; // 图像高度
            Int32 PixelWidth = bmp.Width;   // 图像宽度
            Int32 Stride = ((PixelWidth * 3 + 3) >> 2) << 2;    // 跨距宽度
            Byte[] Pixels = new Byte[PixelHeight * Stride];

            // 锁定位图到系统内存
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(bmpData.Scan0, Pixels, 0, Pixels.Length);  // 从非托管内存拷贝数据到托管内存
            bmp.UnlockBits(bmpData);    // 从系统内存解锁位图

            // Bgr555格式为 X RRRRR GGGGG BBBBB
            Int32 TargetStride = ((PixelWidth + 1) >> 1) << 2;  // 每个像素占2字节，且跨距要求4字节对齐
            Byte[] TargetPixels = new Byte[PixelHeight * TargetStride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Index = i * Stride;
                Int32 Loc = i * TargetStride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    Byte B = Pixels[Index++];
                    Byte G = Pixels[Index++];
                    Byte R = Pixels[Index++];

                    TargetPixels[Loc++] = (Byte)(((G << 2) & 0xe0) | ((B >> 3) & 0x1f));
                    TargetPixels[Loc++] = (Byte)(((R >> 1) & 0x7c) | ((G >> 6) & 3));
                }
            }

            // 创建Bgr555图像
            Bitmap TargetBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format16bppRgb555);

            // 设置位图图像特性
            BitmapData TargetBmpData = TargetBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb555);
            Marshal.Copy(TargetPixels, 0, TargetBmpData.Scan0, TargetPixels.Length);
            TargetBmp.UnlockBits(TargetBmpData);

            return TargetBmp;
        }


        /// <summary>
        /// 将位图转换为彩色数组
        /// </summary>
        /// <param name="bmp">原始位图</param>
        /// <returns>彩色数组</returns>
        public static Color[,] ToColorArray(this Bitmap bmp)
        {
            Int32 PixelHeight = bmp.Height; // 图像高度
            Int32 PixelWidth = bmp.Width;   // 图像宽度
            Int32[] Pixels = new Int32[PixelHeight * PixelWidth];

            // 锁定位图到系统内存
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(bmpData.Scan0, Pixels, 0, Pixels.Length);  // 从非托管内存拷贝数据到托管内存
            bmp.UnlockBits(bmpData);    // 从系统内存解锁位图

            // 将像素数据转换为彩色数组
            Color[,] ColorArray = new Color[PixelHeight, PixelWidth];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    ColorArray[i, j] = Color.FromArgb(Pixels[i * PixelWidth + j]);
                }
            }



            return ColorArray;
        }

        /// <summary>
        /// 将位图转换为灰度数组（256级灰度）
        /// </summary>
        /// <param name="bmp">原始位图</param>
        /// <returns>灰度数组</returns>
        public static Byte[,] ToGrayArray(this Bitmap bmp)
        {
            Int32 PixelHeight = bmp.Height; // 图像高度
            Int32 PixelWidth = bmp.Width;   // 图像宽度
            Int32 Stride = ((PixelWidth * 3 + 3) >> 2) << 2;    // 跨距宽度
            Byte[] Pixels = new Byte[PixelHeight * Stride];

            // 锁定位图到系统内存
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(bmpData.Scan0, Pixels, 0, Pixels.Length);  // 从非托管内存拷贝数据到托管内存
            bmp.UnlockBits(bmpData);    // 从系统内存解锁位图

            // 将像素数据转换为灰度数组
            Byte[,] GrayArray = new Byte[PixelHeight, PixelWidth];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Index = i * Stride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    GrayArray[i, j] = Convert.ToByte((Pixels[Index + 2] * 19595 + Pixels[Index + 1] * 38469 + Pixels[Index] * 7471 + 32768) >> 16);
                    Index += 3;
                }
            }

            return GrayArray;
        }

        /// <summary>
        /// 位图灰度化
        /// </summary>
        /// <param name="bmp">原始位图</param>
        /// <returns>灰度位图</returns>
        public static Bitmap ToGrayBitmap(this Bitmap bmp)
        {
            Int32 PixelHeight = bmp.Height; // 图像高度
            Int32 PixelWidth = bmp.Width;   // 图像宽度
            Int32 Stride = ((PixelWidth * 3 + 3) >> 2) << 2;    // 跨距宽度
            Byte[] Pixels = new Byte[PixelHeight * Stride];

            // 锁定位图到系统内存
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(bmpData.Scan0, Pixels, 0, Pixels.Length);  // 从非托管内存拷贝数据到托管内存
            bmp.UnlockBits(bmpData);    // 从系统内存解锁位图

            // 将像素数据转换为灰度数据
            Int32 GrayStride = ((PixelWidth + 3) >> 2) << 2;
            Byte[] GrayPixels = new Byte[PixelHeight * GrayStride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Index = i * Stride;
                Int32 GrayIndex = i * GrayStride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    GrayPixels[GrayIndex++] = Convert.ToByte((Pixels[Index + 2] * 19595 + Pixels[Index + 1] * 38469 + Pixels[Index] * 7471 + 32768) >> 16);
                    Index += 3;
                }
            }

            // 创建灰度图像
            Bitmap GrayBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format8bppIndexed);

            // 设置调色表
            ColorPalette cp = GrayBmp.Palette;
            for (int i = 0; i < 256; i++) cp.Entries[i] = Color.FromArgb(i, i, i);
            GrayBmp.Palette = cp;

            // 设置位图图像特性
            BitmapData GrayBmpData = GrayBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            Marshal.Copy(GrayPixels, 0, GrayBmpData.Scan0, GrayPixels.Length);
            GrayBmp.UnlockBits(GrayBmpData);

            return GrayBmp;
        }

        /// <summary>
        /// 将灰度数组转换为灰度图像（256级灰度）
        /// </summary>
        /// <param name="grayArray">灰度数组</param>
        /// <returns>灰度图像</returns>
        public static Bitmap GrayArrayToGrayBitmap(Byte[,] grayArray)
        {   // 将灰度数组转换为灰度数据
            Int32 PixelHeight = grayArray.GetLength(0);     // 图像高度
            Int32 PixelWidth = grayArray.GetLength(1);      // 图像宽度
            Int32 Stride = ((PixelWidth + 3) >> 2) << 2;    // 跨距宽度
            Byte[] Pixels = new Byte[PixelHeight * Stride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Index = i * Stride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    Pixels[Index++] = grayArray[i, j];
                }
            }

            // 创建灰度图像
            Bitmap GrayBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format8bppIndexed);

            // 设置调色表
            ColorPalette cp = GrayBmp.Palette;
            for (int i = 0; i < 256; i++) cp.Entries[i] = Color.FromArgb(i, i, i);
            GrayBmp.Palette = cp;

            // 设置位图图像特性
            BitmapData GrayBmpData = GrayBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            Marshal.Copy(Pixels, 0, GrayBmpData.Scan0, Pixels.Length);
            GrayBmp.UnlockBits(GrayBmpData);

            return GrayBmp;
        }

        /// <summary>
        /// 将二值化数组转换为二值化图像
        /// </summary>
        /// <param name="binaryArray">二值化数组</param>
        /// <returns>二值化图像</returns>
        public static Bitmap BinaryArrayToBinaryBitmap(Byte[,] binaryArray)
        {   // 将二值化数组转换为二值化数据
            Int32 PixelHeight = binaryArray.GetLength(0);
            Int32 PixelWidth = binaryArray.GetLength(1);
            Int32 Stride = ((PixelWidth + 31) >> 5) << 2;
            Byte[] Pixels = new Byte[PixelHeight * Stride];
            for (Int32 i = 0; i < PixelHeight; i++)
            {
                Int32 Base = i * Stride;
                for (Int32 j = 0; j < PixelWidth; j++)
                {
                    if (binaryArray[i, j] != 0)
                    {
                        Pixels[Base + (j >> 3)] |= Convert.ToByte(0x80 >> (j & 0x7));
                    }
                }
            }

            // 创建黑白图像
            Bitmap BinaryBmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format1bppIndexed);

            // 设置调色表
            ColorPalette cp = BinaryBmp.Palette;
            cp.Entries[0] = Color.Black;    // 黑色
            cp.Entries[1] = Color.White;    // 白色
            BinaryBmp.Palette = cp;

            // 设置位图图像特性
            BitmapData BinaryBmpData = BinaryBmp.LockBits(new Rectangle(0, 0, PixelWidth, PixelHeight), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
            Marshal.Copy(Pixels, 0, BinaryBmpData.Scan0, Pixels.Length);
            BinaryBmp.UnlockBits(BinaryBmpData);

            return BinaryBmp;
        }
    }
}