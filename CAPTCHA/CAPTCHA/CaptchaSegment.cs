using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CAPTCHA;
using Preprocess;


namespace CaptchaSegment
{
    public class Boundary
    {
        private Point _rightUpPoint;

        public Point rightUpPoint
        {
            get { return _rightUpPoint; }
            set { _rightUpPoint = value; }
        }

        private Point _leftDownPoint;

        public Point leftDownPoint
        {
            get { return _leftDownPoint; }
            set { _leftDownPoint = value; }
        }
    }

    public class SegAreaInfo
    {
        //已分割区域包含的有效字符像素的数量
        private int _blackPixCount;

        public int blackPixCount
        {
            get { return _blackPixCount; }
            set { _blackPixCount = value; }
        }

        private Boundary _segAreaB;

        public Boundary segAreaB
        {
            get { return _segAreaB; }
            set { _segAreaB = value; }
        }
       
        private  byte[,] _binaryArr;

        public byte[,] binaryArr
        {
            get { return _binaryArr; }
            set { _binaryArr = value; }
        }
    }


    

    public class CaptchaSegment
    {
        //记录当前处理验证码字符有效区域
        public static Boundary ImgBoundary = new Boundary();

        public static int ImageHeight = CAPTCHA.Program.Height;

        public static int ImageWidth = CAPTCHA.Program.Width;

        //当前验证码图片中包含的有效字符的总像素数
        public static int bPixSum = 0;

        //记录当前验证码的二值化数组
        public static Byte[,] BinaryArray = new Byte[ImageHeight, ImageWidth];

        //记录当前验证码的分割路径
        public static ArrayList SegPathList= new ArrayList();

        //记录当前已分割区域的相关信息：分割区域所包含的黑色像素数目;已分割区域的与baseline的交点;已分割区域的与meanine的交点;
        //已分割区域的最高点(离原点最远的，X坐标较大值);已分割区域的最低点(离原地最低点，X坐标较小值)
        public static ArrayList SegAreaList = new ArrayList();

        //记录当前处理验证码的字符笔画宽度
        public static int StrokeWid = 0;

        //记录baseline与meanline的X坐标,//Point.x: basePoint; Point.y: meanPoint
        public static Point GuidePoint = new Point();
        /// <summary>
        /// 将输入的含粘粘字符的图片转换为分割后的字符图片
        /// </summary>
        /// <param name="imageSrcPath">粘粘字符图片的路径</param>
        /// <param name="imageSrcPath">分割后图片的存储路径</param>
        /// <returns>字符分割后的图片为位图</returns>
        public static Bitmap CaptchaSegmentFun(Bitmap srcBmp)//, string imageDestPath)
        {
            Byte[,] grayArraySrc = Preprocess.Preprocess.ToGrayArray(srcBmp);

            BinaryArray = Preprocess.Preprocess.Sauvola(grayArraySrc);

            FixBrokenCharacter();

            StrokeWid = Preprocess.Preprocess.GetStrokeWid(ImageHeight, ImageWidth, BinaryArray);

            ImgBoundary = Preprocess.Preprocess.getImgBoundary(ImageHeight, ImageWidth, BinaryArray);

            GetGuidePoint();

            Byte[,] grayArray = new Byte[ImageHeight, ImageWidth];

            for (int i = 0; i < ImageHeight; i++)
            {
                for (int j = 0; j < ImageWidth; j++)
                {
                    grayArray[i, j] = BinaryArray[i, j];

                    if (0 == BinaryArray[i, j])
                    {
                        bPixSum++;
                    }
                }
            }

            grayArray = Preprocess.Preprocess.FillConnectedArea(1, 1, ImageHeight, ImageWidth, grayArray);

            ArrayList loopArray = GetLoopBoundary(grayArray);

            SegByLoop(loopArray, grayArray);
            
            
            int times = 0;//在loop特征分割完成后，允许根据guideline分割的次数，现暂定为5次
            while ((SegPathList.Count < 5) && (times < 5))
            {
                GetMergedArea();

                SegAreaList.Clear();

                for (int k = 0; k < (SegPathList.Count - 1); k++)
                {
                    ArrayList pathLeft = (ArrayList)SegPathList[k];
                    ArrayList pathRight = (ArrayList)SegPathList[k + 1];

                    SegAreaInfo aInfo = GetSegInfo(pathLeft, pathRight);

                    SegAreaList.Add(aInfo);
                }


                times++;
            }
            
            
            return (GetSegImg());
        }


        /// <summary>
        /// 修补有笔画缺陷的字符,修补后的二值化数组仍存储在BinaryArray中
        /// </summary>
        public static void FixBrokenCharacter()
        {
            /*            
               5	7	 8			
               5	X	 1
               4	3	 2
            */
            // 1 2 3 4 5 6 7 8
            // 1 0 0 1 0 0 0 0  144
            // 1 0 0 0 1 0 0 0  136
            // 1 0 0 0 0 1 0 0  132
            // 0 1 0 0 1 0 0 0  72
            // 0 1 0 0 0 1 0 0  68
            // 0 1 0 0 0 0 1 0  66
            // 0 0 1 0 0 1 0 0  36
            // 0 0 1 0 0 0 1 0  34
            // 0 0 1 0 0 0 0 1  33
            // 0 0 0 1 0 0 1 0  18
            // 0 0 0 1 0 0 0 1  17
            // 0 0 0 0 1 0 0 1  9

            int[] templateFix = new int[] {34,136 };
            //{9,17,18,33,34,36,66,68,72,132,136,144};

           // Byte[,] fixArray = new Byte[ImageHeight, ImageWidth];

            int i = 0, j = 0;

            for(i = 0; i < ImageHeight; i ++)
            {
                for (j = 0; j < ImageWidth; j ++)
                {
                    if (0 == BinaryArray[i, j])
                    {
                        BinaryArray[i, j] = 1;
                    }
                    else
                    {
                        BinaryArray[i, j] = 0;
                    }
                }
            }

            Byte point1 = 0;
            Byte point2 = 0;
            Byte point3 = 0;
            Byte point4 = 0;
            Byte point5 = 0;
            Byte point6 = 0;
            Byte point7 = 0;
            Byte point8 = 0;

            for(i = 1; i < (ImageHeight - 1); i ++)
            {
                for (j = 1; j < (ImageWidth - 1); j ++ )
                {
                    if (0 == BinaryArray[i, j])
                    {
                        point1 = BinaryArray[i, j + 1];
                        point2 = BinaryArray[i + 1, j + 1];
                        point3 = BinaryArray[i + 1, j];
                        point4 = BinaryArray[i + 1, j - 1];

                        point5 = BinaryArray[i, j - 1];
                        point6 = BinaryArray[i - 1, j - 1];
                        point7 = BinaryArray[i - 1, j];
                        point8 = BinaryArray[i - 1, j + 1];

                        int nValue = (point1 << 7) + (point2 << 6) + (point3 << 5) + (point4 << 4) + (point5 << 3)
                            + (point6 << 2) + (point7 << 1) + point8;
                        if (templateFix.Contains(nValue))
                        {
                            BinaryArray[i, j] = 1;
                        }
                    }
                }
            }

            for (i = 0; i < ImageHeight; i++)
            {
                for (j = 0; j < ImageWidth; j++)
                {
                    if (0 == BinaryArray[i, j])
                    {
                        BinaryArray[i, j] = 255;
                    }
                    else
                    {
                        BinaryArray[i, j] = 0;
                    }
                }
            }
        }


        /// <summary>
        /// 获取验证码图片的baseline和meanline在X轴上的坐标
        /// </summary>
        public static void GetGuidePoint()
        {
            int[] pointArray = new int[ImgBoundary.rightUpPoint.Y];          
			
			int i = 0, j = 0;

            //找到每一列从字符底部到字符顶部遇到的第一个字符像素的X坐标值
            for (j = ImgBoundary.leftDownPoint.Y; j < ImgBoundary.rightUpPoint.Y; j++)
            {
                pointArray[j] = 0;

                for (i = ImgBoundary.rightUpPoint.X; i > ImgBoundary.leftDownPoint.X; i--)
                {
                    if (0 == BinaryArray[i,j])
                    {
                        pointArray[j] = i;
                        break;
                    }
                }
            }

            //找出上述X坐标值中数量最多的X值作为baseline
            Int32[] Histogram = new Int32[ImgBoundary.rightUpPoint.Y];
            Array.Clear(Histogram, 0, ImgBoundary.rightUpPoint.Y);     
            foreach (Int32 b in pointArray)
            {
                if(b != 0)
                {
                    Histogram[b]++;                
                }
            }
            
            //根据研究对象的特征，高于baseline三个像素以上的区域才会被认为是在baseline以下区域，因此将得到的坐标值+3个像素点作为baseline的值
            GuidePoint.X = Array.IndexOf(Histogram, Histogram.Max());// +3;

            //找到每一列从字符顶部到字符底部遇到的第一个字符像素的X坐标值
            for (j = ImgBoundary.leftDownPoint.Y; j < ImgBoundary.rightUpPoint.Y; j++)
            {
                pointArray[j] = 0;

                for (i = ImgBoundary.leftDownPoint.X; i < ImgBoundary.rightUpPoint.X; i++)
                {
                    if (0 == BinaryArray[i,j])
                    {
                        pointArray[j] = i;
                        break;
                    }
                }
            }

            //找到上述X值中数量最多的点作为meanline的参考点
            Array.Clear(Histogram, 0, ImgBoundary.rightUpPoint.X);     // 初始化
			
            foreach (Int32 b in pointArray)
            {
                if(b != 0)
                {
                    Histogram[b]++;                 // 统计直方图
                }
            }

            //小于meanline 2个像素的会被认为是meanline以上的区域，故直接将meanline减2个像素
            Int32 midValIndex = Array.IndexOf(Histogram, Histogram.Max());

            int tmpVal = ImgBoundary.leftDownPoint.X + (ImgBoundary.rightUpPoint.X - ImgBoundary.leftDownPoint.X) / 5;

            //防止meanline以上没有有效像素，故并不直接以计算值作为guidePoint		
			if(midValIndex > tmpVal)
			{
				GuidePoint.Y = midValIndex;
			}
			else
			{
				GuidePoint.Y = tmpVal;
			}
        }


        /// <summary>
        /// 根据仅loop为白色的灰度数组定位每个loop的坐标范围
        /// </summary>
        public static ArrayList GetLoopBoundary(Byte[,] grayArray)
        {
            int i = 0, j = 0, k = 0;

            int[] wHistogram = new int[ImageWidth];
            int[] hHistogram = new int[ImageHeight];
 
            //对字符所包含的封闭区间进行竖直投影
            for (i = ImgBoundary.leftDownPoint.X; i < ImgBoundary.rightUpPoint.X; i++)
            {
                for (j = ImgBoundary.leftDownPoint.Y; j < ImgBoundary.rightUpPoint.Y; j++)
                {
                    if (255 == grayArray[i, j])
                    {
                        wHistogram[j] ++;
                    }
                }
            }

            ArrayList loopArray = new ArrayList();//compose of rightUpPoint and leftDownPoint

            j = ImgBoundary.leftDownPoint.Y;
 
            //根据封闭区间的竖直投影获取封闭区间的左右与上下极限位置
            while (j < ImgBoundary.rightUpPoint.Y)
            {
                if(wHistogram[j] != 0)
                {
                    Boundary loop = new Boundary();                    
   
                    //高为X轴，宽为Y轴，图片的左上角为原点。
                    //Y轴方向离原点较远的顶点,X轴方向离原点较远的顶点，这两个点为rightUp
                    Point posRightUp = new Point();
                    //Y轴方向离原点较近的顶点,X轴方向离原点较近的顶点，这两个点为LeftDown
                    Point posLeftDown = new Point();

                    posLeftDown.Y = j;

                    while ((wHistogram[j] != 0) && (j < ImgBoundary.rightUpPoint.Y))
                    {
                        j ++;
                    }

                    posRightUp.Y = j - 1;

                    Array.Clear(hHistogram, 0, ImageHeight); 
                 
                    //在已确定的左右区间内对封闭区域进行水平方向的投影
                    for (j = posLeftDown.Y; j < posRightUp.Y; j++)
                    {
                        for (i = ImgBoundary.leftDownPoint.X; i < ImgBoundary.rightUpPoint.X; i++)
                        {
                            if (255 == grayArray[i, j])
                            {
                                hHistogram[i]++;
                            }
                        }
                    }

                    //寻找水平投影起点的i的初始值
                    i = ImgBoundary.leftDownPoint.X;

                    while ((0 == hHistogram[i]) && (i < ImgBoundary.rightUpPoint.X))
                    {
                        i++;
                    }

                    posLeftDown.X = i;

                    i = ImgBoundary.rightUpPoint.X;

                    while ((0 == hHistogram[i]) && (i > ImgBoundary.leftDownPoint.X))
                    {
                        i--;
                    }

                    posRightUp.X = i;

                       
                    loop.leftDownPoint = posLeftDown;
                    loop.rightUpPoint = posRightUp;

                    loopArray.Add(loop);
                        
                }
                j ++;
            }

            //遍历找到的封闭区间内的像素，至少包含一个8邻域的区域被认为是有效封闭区间，否则将被滤除
            //此处的处理方式需要根据实际验证码类型中loop的大小酌情调整，
            for (k = 0; k < loopArray.Count; k ++ )
            {
                Boundary tmpLoop = (Boundary)loopArray[k];

                bool tmpFlg = true;

                for (i = tmpLoop.leftDownPoint.X + 1; (i < tmpLoop.rightUpPoint.X - 1) && (tmpFlg); i ++ )
                {
                    for (j = tmpLoop.leftDownPoint.Y + 1; (j < tmpLoop.rightUpPoint.Y - 1) && (tmpFlg); j++)
                    {
                        if ((255 == grayArray[i,j]) && (255 == grayArray[i,j - 1]) && (255 == grayArray[i,j + 1])
                            && (255 == grayArray[i + 1, j]) && (255 == grayArray[i + 1, j - 1]) && (255 == grayArray[i + 1, j + 1])
                            && (255 == grayArray[i - 1, j]) && (255 == grayArray[i - 1, j - 1]) && (255 == grayArray[i - 1, j + 1]))
                        {
                            tmpFlg = false;
                        }
                    }
                }

                if (tmpFlg)
                {
                    loopArray.RemoveAt(k);

                    k--;
                }
            }

            return loopArray;
        }

        /// <summary>
        /// 根据两条分割路径获取已分割路径之间区域的如下信息:
        /// 1.两个路径之间包含的有效字符的像素点数(黑色像素的数量)
        /// 2.已分割区域的与baseline的交点
        /// 3.已分割区域的与meanine的交点
        /// 4.已分割区域的最高点(离原点最远的，X坐标较大值)
        /// 5.已分割区域的最低点(离原地最低点，X坐标较小值)
        /// </summary>
        public static SegAreaInfo GetSegInfo(ArrayList pathLeft, ArrayList pathRight)
        {
            //遍历两条路径之间的区域用
            Point posRight = new Point();
            Point posLeft = new Point();

            int widthRight = 0;
            int widthLeft = 0;

            int heightRight = 0;
            int heightLeft = 0;

            int indexLeft = 0;
            int indexRight = 0;

            int posCountLeft = 0;
            int posCountRight = 0;

            int bPixCount = 0;

            SegAreaInfo segArea = new SegAreaInfo();

            Boundary tmpBoundary = new Boundary();

            //用于存储已分割区域的二值化数组
            Byte[,] binaryArr = new Byte[ImageHeight, ImageWidth];

            for(int m = 0; m < ImageHeight; m++)
            {
                for (int n = 0; n < ImageWidth;n ++ )
                {
                    binaryArr[m, n] = 255;
                }
            }

            bPixCount = 0;

            posCountLeft = pathLeft.Count;
            posCountRight = pathRight.Count;

            indexLeft = 0;
            indexRight = 0;

            //两条路径之间的点数可能不一致，可能会出现同一个X值对应多个Y值的状况
            while ((indexLeft < posCountLeft) && (indexRight < posCountRight))
            {
                posRight = (Point)pathRight[indexRight];
                posLeft = (Point)pathLeft[indexLeft];

                indexLeft++;
                indexRight++;

                widthLeft = posLeft.Y;
                widthRight = posRight.Y;

                heightRight = posLeft.X;
                heightLeft = posRight.X;

                //路径中可能会出现多个宽度值对应同一高度值的状况，找到同一高度值下最左侧的宽度值
                while (indexLeft < posCountLeft)
                {
                    posLeft = (Point)pathLeft[indexLeft];

                    if (widthLeft != posLeft.X)
                    {
                        break;
                    }
                    else
                    {
                        if (widthLeft > posLeft.Y)
                        {
                            widthLeft = posLeft.Y;
                        }
                        indexLeft++;
                    }
                }

                //路径中可能会出现多个宽度值对应同一高度值的状况，找到同一高度值下最右侧的宽度值
                while (indexRight < posCountRight)
                {
                    posRight = (Point)pathRight[indexRight];

                    if (heightRight != posRight.X)
                    {
                        break;
                    }
                    else
                    {
                        if (widthRight < posRight.Y)
                        {
                            widthRight = posRight.Y;
                        }
                        indexRight++;
                    }
                }

                //从左至右，从上至下，遍历两条路径之间的像素点
                for (Int32 y = widthLeft; y < widthRight; y++)
                {
                    binaryArr[heightLeft, y] = BinaryArray[heightLeft, y];
                                  
                    if (0 == BinaryArray[heightLeft, y])
                    {
                        //将两条路径之间的有效像素点的总数
                        bPixCount++;
                    }
                }                  
            }

            tmpBoundary = Preprocess.Preprocess.getImgBoundary(ImageHeight, ImageWidth, binaryArr);

            segArea.blackPixCount = bPixCount;

            segArea.segAreaB = tmpBoundary;

            segArea.binaryArr = binaryArr;

            return segArea;
        }


       /// <summary>
	   /// 根据侦测到的LOOP区域进行分割
	   /// </summary>
	   /// <param name="loopArray">当前验证码图片中LOOP区域的信息集合</param>
	   /// <param name="grayArray">有效字符为黑色，loop区域为白色，背景为灰色的灰度数组</param>
        public static void SegByLoop(ArrayList loopArray,  Byte[,] grayArray)
		{
            int width = ImgBoundary.rightUpPoint.Y - ImgBoundary.leftDownPoint.Y;
			
            //若loop左侧与图像左侧的距离，或者loop右侧与图像右侧的距离小于该值，那么该loop位于最左侧或最右侧，它将只有一个分割点
			int minW = width / 7;

            //当前分割点
            Point pos = new Point();

            //上一个分割点，若两个分割点之间的距离小于minW，那么新的分割点将无效
            Point oldPos = new Point();

            //ArrayList SegPathList = new ArrayList();

            int i = 0;

            ArrayList pathListFirst = new ArrayList();
            //将图像最左侧的点所在直线作为第一条分割路径
            for (i = ImgBoundary.leftDownPoint.X; i < ImgBoundary.rightUpPoint.X; i++)
            {
                pos.X = i;

                pos.Y = ImgBoundary.leftDownPoint.Y;

                pathListFirst.Add(pos);
            }

            SegPathList.Add(pathListFirst);

            oldPos = pos;

			for(int k = 0; k < loopArray.Count; k ++)
			{
                ArrayList pathListUp = new ArrayList();

                ArrayList pathListDown = new ArrayList();

                Boundary tmpLoop = (Boundary)loopArray[k];

                //判断当前loop所在位置与baseline的关系，正常状况下loop应该都在baseline以上区域，位于baseline以上区域的部分按正常方式取其分割点
                if (tmpLoop.rightUpPoint.X <= GuidePoint.X)
                {
                    //此处minW阈值可以进行调节，调节的条件是确保最左侧与最右侧的点可以被包含进相应的条件，否则程序有可能出错
                    if ((tmpLoop.leftDownPoint.Y - StrokeWid - ImgBoundary.leftDownPoint.Y) < minW)
                    {
                        pos = GetSementLoopPoint(true, tmpLoop, grayArray);                    
                    }
                    else if ((ImgBoundary.rightUpPoint.Y - StrokeWid - tmpLoop.rightUpPoint.Y) < minW)
                    {
                        pos = GetSementLoopPoint(false, tmpLoop, grayArray);
                    }
                    else
                    {
                        pos = GetSementLoopPoint(false, tmpLoop, grayArray);
                        //应该是新分割点左侧点与旧分割点右侧点的距离？_20150814
                        if ((pos != oldPos) && (pos.Y - oldPos.Y >= minW))
                        {
                            pathListUp = dropFallUp(pos);

                            pathListDown = dropFallDown(pos);

                            for (i = 0; i < pathListUp.Count; i++)
                            {
                                pathListDown.Add(pathListUp[i]);
                            }

                            SegPathList.Add(pathListDown);
                        }

                        oldPos = pos;

                        pos = GetSementLoopPoint(true, tmpLoop, grayArray);               
                    }

                    if ((pos != oldPos) && (pos.Y - oldPos.Y < minW))
                    {
                        continue;
                    }

                    pathListUp = dropFallUp(pos);

                    pathListDown = dropFallDown(pos);

                    for (i = 0; i < pathListUp.Count; i++)
                    {
                        pathListDown.Add(pathListUp[i]);
                    }

                    SegPathList.Add(pathListDown);

                    oldPos = pos;
                }
                else//若其有部分区域位于baseline以下，那么说明当前loop倾斜严重，分割点将直接取该loop与baseline的交叉点作为分割点
                {
                    Point leftP = new Point();
                    Point rightP = new Point();

                    for (i = tmpLoop.leftDownPoint.Y; i < tmpLoop.rightUpPoint.Y; i ++ )
                    {
                        if (255 == grayArray[GuidePoint.X,i])
                        {
                            if(i - StrokeWid < 0)
                            {
                                leftP.Y = 0;
                            }
                            else
                            {
                                leftP.Y = i - StrokeWid;
                            }
                            
                            break;
                        }
                    }

                    for (i = tmpLoop.rightUpPoint.Y; i > tmpLoop.leftDownPoint.Y; i--)
                    {
                        if (255 == grayArray[GuidePoint.X, i])
                        {
                            if (i + StrokeWid > ImageWidth)
                            {
                                rightP.Y = ImageWidth - 1;
                            }
                            else
                            {
                                rightP.Y = i + StrokeWid;
                            }
                            break;
                        }
                    }

                    leftP.X = GuidePoint.X;
                    rightP.X = GuidePoint.X;

                    //此处minW阈值可以进行调节，调节的条件是确保最左侧与最右侧的点可以被包含进相应的条件，否则程序有可能出错
                    if ((leftP.Y - ImgBoundary.leftDownPoint.Y) < minW)
                    {
                        pos = rightP;
                    }
                    else if ((ImgBoundary.rightUpPoint.Y - rightP.Y) < minW)
                    {
                        pos = leftP;
                    }
                    else
                    {
                        pos = leftP;

                        if ((pos != oldPos) && (pos.Y - oldPos.Y >= minW))
                        {
                            pathListUp = dropFallUp(pos);

                            pathListDown = dropFallDown(pos);

                            for (i = 0; i < pathListUp.Count; i++)
                            {
                                pathListDown.Add(pathListUp[i]);
                            }

                            SegPathList.Add(pathListDown);
                        }

                        oldPos = pos;

                        pos = rightP;
                    }

                    if ((pos != oldPos) && (pos.Y - oldPos.Y < minW))
                    {
                        continue;
                    }

                    pathListUp = dropFallUp(pos);

                    pathListDown = dropFallDown(pos);

                    for (i = 0; i < pathListUp.Count; i++)
                    {
                        pathListDown.Add(pathListUp[i]);
                    }

                    SegPathList.Add(pathListDown);

                    oldPos = pos;
                }
			}

            ArrayList pathListLast = new ArrayList();
            //将图像最右侧的点所在直线作为最后一条分割路径
            for (i = ImgBoundary.leftDownPoint.X; i < ImgBoundary.rightUpPoint.X; i++)
            {
                Point newPos = new Point();

                newPos.X = i;

                newPos.Y = ImgBoundary.rightUpPoint.Y;

                pathListLast.Add(newPos);
            }
            SegPathList.Add(pathListLast);

            //若分割路径小于5，那么获取已分割区域的相关信息，用于指导后续根据Guideline继续分割
            if (SegPathList.Count < 5)
            {
                for (i = 0; i < (SegPathList.Count - 1); i++ )
                {
                    ArrayList pathLeft = (ArrayList)SegPathList[i];
                    ArrayList pathRight = (ArrayList)SegPathList[i + 1];

                    SegAreaInfo aInfo = GetSegInfo(pathLeft, pathRight);

                    SegAreaList.Add(aInfo);
                }
            }
		}

       /// <summary>
 	   /// 根据指定loop的信息获取相应的分割点
 	   /// </summary>
 	   /// <param name="beRight">用于标记当前需要确认的分割点在LOOP的左侧或右侧，true:右侧；false:左侧</param>
       /// <param name="Loop">当前研究的LOOP的坐标范围</param>
 	   /// <param name="grayArray">有效字符为黑色，loop区域为白色，背景为灰色的灰度数组</param>
        public static Point GetSementLoopPoint(bool beRight, Boundary Loop,  Byte[,] grayArray)
        {
            Point segPoint = new Point();
     
            int tmpH = 0;
            
            int tmpW = 0;

            if (beRight)
            {
            	//确认LOOP右侧的分割点，先找到LOOP位于最右侧点的坐标
                for (tmpH = Loop.leftDownPoint.X; tmpH < Loop.rightUpPoint.X; tmpH++)
                {
                    if (255 == grayArray[tmpH, Loop.rightUpPoint.Y])
                    {
                        break;
                    }
                }
                
                //以LOOP最右侧点的高度坐标为分割点的高度坐标；以右侧点的宽度坐标+笔画宽度作为分割点的宽度坐标
                segPoint.X = tmpH;
 
                tmpW = Loop.rightUpPoint.Y + StrokeWid;
                
                //若确认的分割点处为灰色，那么从该点开始逐点后退找到紧贴LOOP的灰色像素作为分割点
                while(128 == grayArray[tmpH,tmpW])
                {
                	tmpW --;
                }
                
                segPoint.Y = tmpW + 1;
            }
            else
            {
            	//确认LOOP左侧的分割点，先找到LOOP位于最左侧点的坐标
                for (tmpH = Loop.leftDownPoint.X; tmpH < Loop.rightUpPoint.X; tmpH++)
                {
                    if (255 == grayArray[tmpH, Loop.leftDownPoint.Y])
                    {
                        break;
                    }
                }

                //以LOOP最左侧点的高度坐标为分割点的高度坐标；以左侧点的宽度坐标-笔画宽度作为分割点的宽度坐标
                segPoint.X = tmpH;

                tmpW = Loop.leftDownPoint.Y - StrokeWid;
                
                //若确认的分割点处为灰色，那么从该点开始逐点前进找到紧贴LOOP的灰色像素作为分割点
				while(128 == grayArray[tmpH,tmpW])
				{
					tmpW ++;
				}
				
				segPoint.Y = tmpW - 1;             
            }

            return segPoint;
        }

        /// <summary>
        ///根据分割路径分割粘粘字符并另存为位图
        /// </summary>
        public static Bitmap GetSegImg()
        {
            int segCount = SegPathList.Count;

            int locationVal = ImageWidth / (segCount - 1);

            int locationPos = 0;

            int iniWidthLeft = 0;

            Byte[,] divideArray = new Byte[ImageHeight, ImageWidth];

            for (Int32 x = 0; x < ImageHeight; x++)
            {
                for (Int32 y = 0; y < ImageWidth; y++)
                {
                    divideArray[x, y] = 255;
                }
            }

            Point divPosRight = new Point();

            Point divPosLeft = new Point();

            ArrayList pathListLeft = new ArrayList();
            ArrayList pathListRight = new ArrayList();

            int indexWidthRight = 0;
            int indexWidthLeft = 0;

            int indexHeightRight = 0;
            int indexHeightLeft = 0;

            int pointIndexLeft = 0;
            int pointIndexRight = 0;

            int posCountLeft = 0;
            int posCountRight = 0;

            for (int i = 0; i < segCount - 1; i++)
            {
                //用于记录分割区域所在的范围，Point.X为分割范围的左侧，Point.Y为分割范围的右侧
               // Point segArea = new Point();

                pathListLeft = (ArrayList)SegPathList[i];
                pathListRight = (ArrayList)SegPathList[i + 1];

                posCountLeft = pathListLeft.Count;
                posCountRight = pathListRight.Count;

                //遍历左侧路径中的点，找到左侧路径点中最小的宽度值，用于辅助分割后字符的定位
                divPosRight = (Point)pathListRight[0];
                iniWidthLeft = divPosRight.Y;

                divPosLeft = (Point)pathListLeft[0];

                for (pointIndexLeft = 0; pointIndexLeft < posCountLeft; pointIndexLeft++)
                {
                    divPosLeft = (Point)pathListLeft[pointIndexLeft];

                    if (iniWidthLeft > divPosLeft.Y)
                    {
                        iniWidthLeft = divPosLeft.Y;
                    }
                }
 
                locationPos = 5 + locationVal * i;

                pointIndexLeft = 0;
                pointIndexRight = 0;

                while ((pointIndexLeft < posCountLeft) && (pointIndexRight < posCountRight))
                {
                    //遍历路径中每一个高度值，找到每一个高度值对应的宽度左右极限，将此范围内的像素点重新进行定位以分割字符
                    divPosRight = (Point)pathListRight[pointIndexRight];
                    divPosLeft = (Point)pathListLeft[pointIndexLeft];

                    indexWidthLeft = divPosLeft.Y;
                    indexWidthRight = divPosRight.Y;

                    indexHeightRight = divPosRight.X;
                    indexHeightLeft = divPosLeft.X;

                    pointIndexLeft++;
                    pointIndexRight++;

                    //路径中可能会出现多个宽度值对应同一高度值的状况，找到同一高度值下最左侧的宽度值
                    while (pointIndexLeft < posCountLeft)
                    {
                        divPosLeft = (Point)pathListLeft[pointIndexLeft];

                        if (indexHeightLeft != divPosLeft.X)
                        {
                            break;
                        }
                        else
                        {
                            if (indexWidthLeft > divPosLeft.Y)
                            {
                                indexWidthLeft = divPosLeft.Y;
                            }
                            pointIndexLeft++;
                        }
                    }

                    //路径中可能会出现多个宽度值对应同一高度值的状况，找到同一高度值下最右侧的宽度值
                    while (pointIndexRight < posCountRight) 
                    {
                        divPosRight = (Point)pathListRight[pointIndexRight];

                        if (indexHeightRight != divPosRight.X)
                        {
                            break;
                        }
                        else
                        {
                            if (indexWidthRight < divPosRight.Y)
                            {
                                indexWidthRight = divPosRight.Y;
                            }
                            pointIndexRight++;
                        }
                    }
                                                        
                    int tmpVal = 0;

                    for (Int32 y = indexWidthLeft; y < indexWidthRight; y++)
                    {
                        tmpVal = y - iniWidthLeft + locationPos;

                        if (tmpVal < ImageWidth)
                        {
                            divideArray[indexHeightRight, tmpVal] = BinaryArray[indexHeightRight, y];
                        }
                        else
                        {
                            //error;
                        }
                    }                   
                }
            }

            Bitmap GrayBmp = Preprocess.Preprocess.BinaryArrayToBinaryBitmap(divideArray);

            return GrayBmp;
        }

        /// <summary>
        ///根据loop分割后的区块判断是否还有粘连字符，若有则根据guideLine原则继续分割
        /// </summary>
        public static void GetMergedArea()//divideInfo divideData, Point guideLine, Byte[,] BinaryArray, ImgBoundary Boundary)
        {
            int areaNum = SegAreaList.Count;

            //记录单个粘连区域内独立有效的竖直投影区域
            ArrayList proLoops = new ArrayList();

            //记录当前粘连区域的Y轴范围
            SegAreaInfo mAreaRange = new SegAreaInfo();
            SegAreaInfo mAreaRange_1 = new SegAreaInfo();
            SegAreaInfo mAreaRange_2 = new SegAreaInfo();
           
            //记录不同区块的黑色像素数，用于辅助判断粘连字符区域
            int blackCount_0 = 0;
            int blackCount_1 = 0;
            int blackCount_2 = 0;

            //记录当前验证码图片中单个字符的平均像素
            int bThVal = bPixSum / 4; 

           // int newPathPos = 0;//记录新的分割路径该插入的位置

            switch (areaNum)
            {
                case 1://当前验证码仍有4个字符粘连

                    mAreaRange = (SegAreaInfo)SegAreaList[0];

                    SegByGuideLine(mAreaRange, 1, 4);

                    break;

                case 2://当前验证码有3个字符粘连与1个分割完成的字符；或者2个字符与2个字符粘连
                    //根据两个区域的黑色像素数来判断哪个区块为粘连区
                    mAreaRange = (SegAreaInfo)SegAreaList[0];

                    blackCount_0 = mAreaRange.blackPixCount;

                    mAreaRange_1 = (SegAreaInfo)SegAreaList[1];

                    blackCount_1 = mAreaRange_1.blackPixCount;

                    if (blackCount_0 > (2 * bThVal + bThVal / 2))//dArray[0]为3个字符粘连区域
                    {
                        SegByGuideLine(mAreaRange, 1, 3);
                    }
                    else if (blackCount_1 > (2 * bThVal + bThVal / 2))//dArray[1]为3个字符粘连区域
                    {
                        SegByGuideLine(mAreaRange_1, 2, 3);
                    }
                    else
                    {
                        SegByGuideLine(mAreaRange, 1, 2);
                    }

                    break;

                case 3://当前验证码有2个字符粘连与2个独立字符，根据区域黑色像素数来找出粘连区
                    mAreaRange = (SegAreaInfo)SegAreaList[0];

                    blackCount_0 = mAreaRange.blackPixCount;

                    mAreaRange_1 = (SegAreaInfo)SegAreaList[1];

                    blackCount_1 = mAreaRange_1.blackPixCount;

                    mAreaRange_2 = (SegAreaInfo)SegAreaList[2];

                    blackCount_2 = mAreaRange_2.blackPixCount;

                    if ((blackCount_0 > blackCount_1) && (blackCount_0 > blackCount_2))//dArray[0]为2个字符粘连区域
                    {
                        SegByGuideLine(mAreaRange,1, 2);
                    }
                    else if ((blackCount_1 > blackCount_0) && (blackCount_1 > blackCount_2))//dArray[1]为2个字符粘连区域
                    {
                        SegByGuideLine(mAreaRange_1, 2, 2);
                    }
                    else
                    {
                        SegByGuideLine(mAreaRange_2, 3, 2);
                    }
                 
                    break;

                default:
                    break;
            }

        }

        /// <summary>
        ///根据粘连区域与Guideline的关系对粘连区域进行分割
        /// </summary>
        /// <param name="mergedArea">粘连区域的信息</param>
        /// <param name="newPos">此次分割得到的路径在总路径中的位置</param>
        /// <param name="num">当前粘连区域包含的字符数</param>
        public static void SegByGuideLine(SegAreaInfo mergedArea, int newPos, int num)
        {
            //循环用变量
            int k = 0;  //SegAreaInfo

            Boundary mBoundary = mergedArea.segAreaB;

            //当前粘连区域粘连字符宽度判定阈值
            int thVal = (mBoundary.rightUpPoint.Y - mBoundary.leftDownPoint.Y) / (num + 1);

            int bThVal = mergedArea.blackPixCount / (num + 2);

            Point segP = new Point();

            ArrayList pathListA = new ArrayList();

            ArrayList pathListB = new ArrayList();

            //用于标注占据三格的字符根据meanline分割的路径是否为有效路径
            bool bValid = false;

            //获取当前粘连区域在meanline之上区域的竖直投影
            // Point point1 = new Point();

            //获取meanline以上区域竖直投影的右上角的点
            segP.X = GuidePoint.Y;
            segP.Y = mBoundary.rightUpPoint.Y;
            VerPro meanArr = Preprocess.Preprocess.VerticalProjection(mBoundary.leftDownPoint, segP, 2, mergedArea.binaryArr);
            int mCount = meanArr.vRange.Count;

            segP.X = GuidePoint.X;
            segP.Y = mBoundary.leftDownPoint.Y;
            VerPro baseArr = Preprocess.Preprocess.VerticalProjection(segP, mBoundary.rightUpPoint, 1, mergedArea.binaryArr);
            int bCount = baseArr.vRange.Count;

            segP.X = GuidePoint.Y;
            segP.Y = mBoundary.leftDownPoint.Y;
            Point tmpPoint = new Point();
            tmpPoint.X = GuidePoint.X;
            tmpPoint.Y = mBoundary.rightUpPoint.Y;
            VerPro midArr = Preprocess.Preprocess.VerticalProjection(segP, tmpPoint, 0, mergedArea.binaryArr);
            int midCount = midArr.vRange.Count;

            //以GuideLine作为分割依据主要是粘连区域占据四线三格中格数的状况         
            if ((mCount > 0) && (bCount > 0))
            {
                //1.若占据三格，则meanline与粘连区域的左侧交点若不位于边界线，则将其作为一个分割点进行分割
                if (meanArr.vInter.X > (mBoundary.leftDownPoint.Y + thVal))
                {
                    segP.X = GuidePoint.Y;
                    segP.Y = meanArr.vInter.X;

                    bValid = SegByMeanLeft(segP, meanArr, mBoundary, bThVal, newPos);
                }

                //若meanline区域分割无效，或者meanline无有效分割点，将以baseline为分割依据
                //取baseline与字符在左侧的第一个交点，若该交点不位于边界，则以该点为分割点
                //若该点位于边界，则以baseline和字符在右侧的第一个交点为分割点
                if ((!bValid) && (baseArr.vInter.X > (mBoundary.leftDownPoint.Y + thVal)))
                {
                    segP.X = GuidePoint.X;
                    segP.Y = baseArr.vInter.X;

                    bValid = SegByBaseLine(segP, meanArr, mBoundary, bThVal, newPos, true);
                }

                //若baseline与meanline的左侧交点均未得到有效分割线，则以baseline和meanline的右侧交点来进行分割  
                //以meanline的右侧交点为分割点
                if ((!bValid) && (meanArr.vInter.Y < (mBoundary.rightUpPoint.Y - thVal)))
                {
                    segP.X = GuidePoint.Y;
                    segP.Y = meanArr.vInter.Y;

                    bValid = SegByMeanRight(segP, meanArr, mBoundary, bThVal, newPos);
                }

                //以baseline的右侧交点为分割点
                if ((!bValid) && (baseArr.vInter.Y < (mBoundary.leftDownPoint.Y - thVal)))
                {
                    segP.X = GuidePoint.X;
                    segP.Y = baseArr.vInter.Y;

                    bValid = SegByBaseLine(segP, meanArr, mBoundary, bThVal, newPos, false);
                }

                //理论上而言，经过上述几项分割后，应该会得到一条有效路径，若仍未得到有效路径，将考虑用投影特征来分割，目前暂不处理此状况

            }
            else if ((mCount > 0) && (0 == bCount))
            {
                //目前粘连区域位于baseline以上的两个格
                //1.若meanline以上区域位于右侧区域，则以meanline与字符的左侧交点为分割点
                if (meanArr.vInter.X > (mBoundary.leftDownPoint.Y + thVal))
                {
                    segP.X = GuidePoint.Y;
                    segP.Y = meanArr.vInter.X;

                    bValid = SegByMeanLeft(segP, meanArr, mBoundary, bThVal, newPos);
                }

                //2.若meanline以上区域位于左侧区域，则以meanline与字符的右侧交点为分割点
                if ((!bValid) && (meanArr.vInter.Y < (mBoundary.rightUpPoint.Y - thVal)))
                {
                    segP.X = GuidePoint.Y;
                    segP.Y = meanArr.vInter.Y;

                    bValid = SegByMeanRight(segP, meanArr, mBoundary, bThVal, newPos);
                }

                //3.若上述两个条件均不成立，则以竖直投影为分割依据
                thVal = (mBoundary.rightUpPoint.Y + mBoundary.leftDownPoint.Y) / 2;
                //3.1 meanline以上区域粘连，以下区域分割，则以其分割区域靠近中间的投影点作为分割点
                if (!bValid)
                {
                    if ((1 == mCount) && (midCount > 1))
                    {
                        segP = (Point)midArr.vRange[0];
                        tmpPoint = (Point)midArr.vRange[1];

                        if (Math.Abs(thVal - segP.Y) < Math.Abs(thVal - tmpPoint.X))
                        {
                            Point pt1 = new Point();

                            pt1.X = GuidePoint.Y;
                            pt1.Y = segP.Y;

                            pathListA = dropFallUp(pt1);

                            pathListB = dropFallDown(pt1);
                        }
                        else
                        {
                            Point pt1 = new Point();
                            pt1.X = GuidePoint.Y;
                            pt1.Y = tmpPoint.X;

                            pathListA = dropFallUp(pt1);

                            pathListB = dropFallDown(pt1);
                        }
                    }
                    else if ((1 == midCount) && (mCount > 1))
                    {
                        //3.2 meanline以上区域分割，以下区域粘连，则以其分割区域靠近中间的投影点作为分割点
                        segP = (Point)meanArr.vRange[0];
                        tmpPoint = (Point)meanArr.vRange[1];

                        if (Math.Abs(thVal - segP.Y) < Math.Abs(thVal - tmpPoint.X))
                        {
                            Point pt1 = new Point();

                            pt1.X = GuidePoint.Y;
                            pt1.Y = segP.Y;

                            pathListA = dropFallUp(pt1);

                            pathListB = dropFallDown(pt1);
                        }
                        else
                        {
                            Point pt1 = new Point();
                            pt1.X = GuidePoint.Y;
                            pt1.Y = tmpPoint.X;

                            pathListA = dropFallUp(pt1);

                            pathListB = dropFallDown(pt1);
                        }
                    }
                    else
                    {
                        //3.3 上下均粘连，则暂不处理
                    }
                    if ((pathListA.Count > 0) && (pathListB.Count > 0))
                    {
                        for (k = 0; k < pathListA.Count; k++)
                        {
                            pathListB.Add(pathListA[k]);
                        }
                        SegPathList.Insert(newPos, pathListB);

                        bValid = true;
                    }

                }
            }
            else if ((0 == mCount) && (bCount > 0))
            {
                //目前粘连区域位于meanline以下的两个格
                //若baseline以下区域位于粘连区域的右侧，则以baseline和字符在左侧的交点为分割点
                if ((!bValid) && (baseArr.vInter.X > (mBoundary.leftDownPoint.Y + thVal)))
                {
                    segP.X = GuidePoint.X;
                    segP.Y = baseArr.vInter.X;

                    bValid = SegByBaseLine(segP, meanArr, mBoundary, bThVal, newPos, true);
                }

                //若baseline以下区域位于粘连区域的左侧，则以baseline和字符在右侧的交点为分割点
                if ((!bValid) && (baseArr.vInter.Y < (mBoundary.rightUpPoint.Y - thVal)))
                {
                    segP.X = GuidePoint.X;
                    segP.Y = baseArr.vInter.Y;

                    bValid = SegByBaseLine(segP, meanArr, mBoundary, bThVal, newPos, false);
                }

                //3.若上述两个条件均不成立，则以竖直投影为分割依据
                if (!bValid)
                {
                    //3.1 baseline以上区域粘连，以下区域分割，则以其分割区域靠近中间的投影点作为分割点
                    //3.2 baseline以上区域分割，以下区域粘连，则以其分割区域靠近中间的投影点作为分割点
                    //3.3 上下均粘连，则暂不处理
                    //3.若上述两个条件均不成立，则以竖直投影为分割依据
                    thVal = (mBoundary.rightUpPoint.Y + mBoundary.leftDownPoint.Y) / 2;
                    //3.1 meanline以上区域粘连，以下区域分割，则以其分割区域靠近中间的投影点作为分割点
                    if (!bValid)
                    {
                        if ((1 == bCount) && (midCount > 1))
                        {
                            segP = (Point)midArr.vRange[0];
                            tmpPoint = (Point)midArr.vRange[1];

                            if (Math.Abs(thVal - segP.Y) < Math.Abs(thVal - tmpPoint.X))
                            {
                                segP.X = GuidePoint.X;
                                pathListA = dropFallUp(segP);

                                pathListB = dropFallDown(segP);
                            }
                            else
                            {
                                tmpPoint.X = GuidePoint.X;
                                pathListA = dropFallUp(tmpPoint);

                                pathListB = dropFallDown(tmpPoint);
                            }
                        }
                        else if ((1 == midCount) && (bCount > 1))
                        {
                            //3.2 meanline以上区域分割，以下区域粘连，则以其分割区域靠近中间的投影点作为分割点
                            segP = (Point)baseArr.vRange[0];
                            tmpPoint = (Point)baseArr.vRange[1];

                            if (Math.Abs(thVal - segP.Y) < Math.Abs(thVal - tmpPoint.X))
                            {
                                pathListA = dropFallUp(segP);

                                pathListB = dropFallDown(segP);
                            }
                            else
                            {
                                pathListA = dropFallUp(tmpPoint);

                                pathListB = dropFallDown(tmpPoint);
                            }
                        }
                        else
                        {
                            //3.3 上下均粘连，则暂不处理
                        }

                        for (k = 0; k < pathListA.Count; k++)
                        {
                            pathListB.Add(pathListA[k]);
                        }

                      //  pathListA = (ArrayList)SegPathList[newPos - 1];

                      //  SegAreaInfo tmpInfo = GetSegInfo(pathListA, pathListB);
                       // SegAreaList.Add(tmpInfo);
                        SegPathList.Insert(newPos, pathListB);

                        bValid = true;
                    }
                }
                else
                {
                    //目前粘连区域位于中间一格，暂不处理
                }
            }
        }

        public static bool SegByMeanLeft(Point segP, VerPro meanArr, Boundary mBoundary, int bThVal, int newPos)
        {
            bool bValid = false;

            //此类交点仅执行从上向下滴落，取meanline以上区域均归为左侧区域
            ArrayList pathListA = dropFallUp(segP);

            ArrayList pathListB = new ArrayList();

            Point point1 = (Point)meanArr.vRange[0];

            int k = 0;

            //从meanline以上区域第一个区块的左下角至与meanline交点在竖直方向以竖直线分割
            for (k = mBoundary.leftDownPoint.X; k < GuidePoint.Y; k++)
            {
                Point tmpP = new Point();

                tmpP.X = k;
                tmpP.Y = point1.X;

                pathListB.Add(tmpP);
            }

            //从从meanline以上区域第一个区块的左下角至meanline交点处在水平方向以横线分割
            for (k = point1.X; k < meanArr.vInter.X; k++)
            {
                Point tmpP = new Point();

                tmpP.X = GuidePoint.Y;
                tmpP.Y = k;

                pathListB.Add(tmpP);
            }

            for (k = 0; k < pathListA.Count; k++)
            {
                pathListB.Add(pathListA[k]);
            }

            //按字符的规则而言，符合该条件的可能是字符A,Z,7,若为字符Z/7，那么分割完成后，左侧区域有一定几率没有有效字符
            //确认按照该规则分割出的左侧区域是否包含有效字符像素
            pathListA.Clear();

            //当前粘连区域左侧竖线作为判断基准
            for (k = mBoundary.leftDownPoint.X; k < mBoundary.rightUpPoint.X; k++)
            {
                Point tmpP = new Point();

                tmpP.X = k;
                tmpP.Y = mBoundary.leftDownPoint.Y;

                pathListA.Add(tmpP);
            }

            SegAreaInfo tmpInfo = GetSegInfo(pathListA, pathListB);

            if (tmpInfo.blackPixCount > bThVal)//当前分割路径为有效路径
            {
                //SegAreaList.Add(tmpInfo);
                SegPathList.Insert(newPos, pathListB);

                bValid = true;
            }

            return bValid;
        }

        public static bool SegByMeanRight(Point segP, VerPro meanArr, Boundary mBoundary, int bThVal, int newPos)
        {
            bool bValid = false;

            ArrayList pathListA = dropFallUp(segP);

            ArrayList pathListB = dropFallDown(segP);

            int k = 0;

            for (k = 0; k < pathListA.Count; k++)
            {
                pathListB.Add(pathListA[k]);
            }

            pathListA.Clear();

            for (k = mBoundary.leftDownPoint.X; k < mBoundary.rightUpPoint.X; k++)
            {
                Point tmpP = new Point();

                tmpP.X = k;
                tmpP.Y = mBoundary.rightUpPoint.Y;

                pathListA.Add(tmpP);
            }

            SegAreaInfo tmpInfo = GetSegInfo(pathListB, pathListA);

            if (tmpInfo.blackPixCount > bThVal)//当前分割路径为有效路径
            {
                //SegAreaList.Add(tmpInfo);
                SegPathList.Insert(newPos, pathListB);

                bValid = true;
            }

            return bValid;
        }

        public static bool SegByBaseLine(Point segP, VerPro baseArr, Boundary mBoundary, int bThVal, int newPos, bool bLeft)
        {
            bool bValid = false;

            //以交点为起点分别执行向上滴落和向下滴落
            ArrayList pathListA = dropFallUp(segP);

            ArrayList pathListB = dropFallDown(segP);

            SegAreaInfo tmpInfo = new SegAreaInfo();

            int k = 0;

            for (k = 0; k < pathListA.Count; k++)
            {
                pathListB.Add(pathListA[k]);
            }

            //按字符的规则而言，符合该条件的可能是字符y,j,那么分割完成后，左侧区域有一定几率没有有效字符
            //确认按照该规则分割出的左侧区域是否包含有效字符像素
            pathListA.Clear();

            //当前粘连区域左侧竖线作为判断基准
            if (bLeft)
            {
                for (k = mBoundary.leftDownPoint.X; k < mBoundary.rightUpPoint.X; k++)
                {
                    Point tmpP = new Point();

                    tmpP.X = k;
                    tmpP.Y = mBoundary.leftDownPoint.Y;

                    pathListA.Add(tmpP);
                }

                tmpInfo = GetSegInfo(pathListA, pathListB);
            }
            else
            {
                for (k = mBoundary.leftDownPoint.X; k < mBoundary.rightUpPoint.X; k++)
                {
                    Point tmpP = new Point();

                    tmpP.X = k;
                    tmpP.Y = mBoundary.rightUpPoint.Y;

                    pathListA.Add(tmpP);
                }

                tmpInfo = GetSegInfo(pathListB, pathListA);
            }

            if (tmpInfo.blackPixCount > bThVal)//当前分割路径为有效路径
            {
                //SegAreaList.Add(tmpInfo);
                SegPathList.Insert(newPos, pathListB);

                bValid = true;
            }

            return bValid;
        }

        public static ArrayList GetProBoundary(Point proRange, bool beBaseline,int iniX, int endX)
        {
            int i = 0, j = 0, k = 0;

            int[] wHistogram = new int[ImageWidth];
            int[] hHistogram = new int[ImageHeight];

            //对字符所包含的封闭区间进行竖直投影
            for (i = iniX; i < endX; i++)
            {
                for (j = proRange.X; j < proRange.Y; j++)
                {
                    if (0 == BinaryArray[i, j])
                    {
                        wHistogram[j]++;
                    }
                }
            }

            ArrayList loopArray = new ArrayList();//compose of rightUpPoint and leftDownPoint

            j = proRange.X;

            //根据封闭区间的竖直投影获取封闭区间的左右与上下极限位罿
            while (j < proRange.Y)
            {
                if (wHistogram[j] != 0)
                {
                    Boundary loop = new Boundary();

                    //高为X轴，宽为Y轴，图片的左上角为原点
                    //Y轴方向离原点较远的顶点，X轴方向离原点较远的顶点，这两个点组成rightUp
                    Point posRightUp = new Point();
                    //Y轴方向离原点较近的顶点，X轴方向离原点较近的顶点，这两个点组成LeftDown
                    Point posLeftDown = new Point();

                    posLeftDown.Y = j;

                    while ((wHistogram[j] != 0) && (j < proRange.Y))
                    {
                        j++;
                    }

                    posRightUp.Y = j - 1;

                    Array.Clear(hHistogram, 0, ImageHeight);

                    //在已确定的左右区间内对封闭区域进行水平方向的投影
                    for (j = posLeftDown.Y; j < posRightUp.Y; j++)
                    {
                        for (i = iniX; i < endX; i++)
                        {
                            if (0 == BinaryArray[i, j])
                            {
                                hHistogram[i]++;
                            }
                        }
                    }
                    //投影区域的左下角的X轴坐标为baseline的X值
                    posLeftDown.X = iniX;

                    i = endX;

                    //寻找投影区域右上角的X轴坐标
                    while ((0 == hHistogram[i]) && (i > iniX))
                    {
                        i--;
                    }

                    posRightUp.X = i;


                    loop.leftDownPoint = posLeftDown;
                    loop.rightUpPoint = posRightUp;

                    loopArray.Add(loop);

                }
                j++;
            }

            if (beBaseline)//若是以baseline为基准的投影，则需要判断该投影区域是否有效
            {
                //遍历找到的封闭区间内的像素，至少包含一丿邻域的区域被认为是有效封闭区间，否则将被滤除
                for (k = 0; k < loopArray.Count; k++)
                {
                    Boundary tmpLoop = (Boundary)loopArray[k];

                    //若投影区域的X轴最大值离baseline的距离小于3个像素，则认为该投影区域为无效区域
                    if (tmpLoop.rightUpPoint.X - iniX < 3)
                    {
                        loopArray.RemoveAt(k);

                        k--;
                    }
                }
            }
            return loopArray;
        }

        

       
        

        /// <summary>
        ///根据谷点获取图片的分割路径，水滴从上向下滴落
        /// </summary>
        /// <param name="valleyCollection">谷点列表</param>
        /// <param name="Boundary">图片中字符所在区域的边界</param>
        /// <param name="BinaryArray">二值化图片数组</param>
        /// <returns>返回粘粘图片的分割路径</returns>
        public static ArrayList dropFallUp(Point iniPos)//,int ImageHeight,int ImageWidth, ImgBoundary Boundary, Byte[,] BinaryArray)
        {
            int yIndex2 = ImgBoundary.rightUpPoint.Y;

            int yIndex1 = ImgBoundary.leftDownPoint.Y;

            int xIndex2 = ImgBoundary.rightUpPoint.X;

            int xIndex1 = ImgBoundary.leftDownPoint.X;

            ArrayList pathList = new ArrayList();

            int iniPointY = iniPos.Y;

            int iniPointX = iniPos.X;//任一分割路径的高度起始点均为有效字符出现的第一个点

            byte leftRightFlg = 0;//该标志位置位时，表示情况5与情况6可能会循环出现

            while ((iniPointX <= xIndex2) && (iniPointY < yIndex2))
            {
                Point newPos = new Point();

                newPos.Y = iniPointY;
                newPos.X = iniPointX;

                pathList.Add(newPos);

                if (((iniPointX + 1) >= ImageHeight) || ((iniPointX - 1) <= 0) || ((iniPointY + 1) >= ImageWidth) || ((iniPointY - 1) <= 0))
                {
                    break;
                }

                Byte pointLeft = BinaryArray[iniPointX, (iniPointY - 1)];
                Byte pointRight = BinaryArray[iniPointX, (iniPointY + 1)];
                Byte pointDown = BinaryArray[(iniPointX + 1), iniPointY];
                Byte pointDownLeft = BinaryArray[(iniPointX + 1), (iniPointY - 1)];
                Byte pointDownRight = BinaryArray[(iniPointX + 1), (iniPointY + 1)];



                if (((0 == pointLeft) && (0 == pointRight) && (0 == pointDown) && (0 == pointDownLeft) && ((0 == pointDownRight)))// all black:11111
                    || ((255 == pointLeft) && (255 == pointRight) && (255 == pointDown) && (255 == pointDownLeft) && ((255 == pointDownRight))//all white:00000
                    || ((0 == pointDownLeft) && (255 == pointDown))))//情况1与情况3  // **10*
                {//down
                    iniPointX = iniPointX + 1;
                    leftRightFlg = 0;
                }
                else if (((255 == pointLeft) && (0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight))//0*011
                    || ((0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight) && (0 == pointLeft) && (0 == pointRight))//11011
                    || ((255 == pointLeft) && (0 == pointDown) && (255 == pointDownLeft) && (255 == pointDownRight)))//0*010
                {//left down //情况2
                    iniPointX = iniPointX + 1;
                    iniPointY = iniPointY - 1;

                    leftRightFlg = 0;
                }
                else if (((0 == pointDown) && (0 == pointDownLeft) && (255 == pointDownRight))//**110
                || ((0 == pointLeft) && (255 == pointRight) && (0 == pointDown) && (255 == pointDownLeft) && (255 == pointDownRight)))//10010
                {//情况4
                    iniPointX = iniPointX + 1;
                    iniPointY = iniPointY + 1;

                    leftRightFlg = 0;
                }
                else if (((255 == pointRight) && (0 == pointDown) && (0 == pointDownLeft) && (0 == pointDownRight))//*0111
                   || ((0 == pointLeft) && (255 == pointRight) && (0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight)))//10011
                {//情况5
                    if (0 == leftRightFlg)
                    {
                        iniPointY = iniPointY + 1;

                        leftRightFlg = 1;

                    }
                    else//标志位为1时说明上一个点出现在情况6，而本次循环的点出现在情况5，此种情况将垂直渗透
                    {
                        iniPointX = iniPointX + 1;


                        leftRightFlg = 0;
                    }
                }
                else if ((255 == pointLeft) && (0 == pointDown) && (0 == pointDownLeft) && (0 == pointDownRight))//01111
                {//情况6
                    if (0 == leftRightFlg)
                    {
                        iniPointY = iniPointY - 1;

                        leftRightFlg = 1;
                    }
                    else//标志位为1时说明上一个点出现在情况5，而本次循环的点出现在情况6，此种情况将垂直渗透
                    {
                        iniPointX = iniPointX + 1;


                        leftRightFlg = 0;
                    }
                }
                else
                {
                    iniPointX = iniPointX + 1;
                }

            }

            return pathList;
        }

        /// <summary>
        ///根据谷点获取图片的分割路径，水滴从下向上滴落
        /// </summary>
        /// <param name="valleyCollection">谷点列表</param>
        /// <param name="Boundary">图片中字符所在区域的边界</param>
        /// <param name="BinaryArray">二值化图片数组</param>
        /// <returns>返回粘粘图片的分割路径</returns>
        public static ArrayList dropFallDown(Point iniPos)//, int ImageHeight, int ImageWidth, ImgBoundary Boundary, Byte[,] BinaryArray)
        {
            int yIndex2 = ImgBoundary.rightUpPoint.Y;

            int yIndex1 = ImgBoundary.leftDownPoint.Y;

            int xIndex2 = ImgBoundary.rightUpPoint.X;

            int xIndex1 = ImgBoundary.leftDownPoint.X;

            ArrayList pathList = new ArrayList();

            int iniPointY = iniPos.Y;

            int iniPointX = iniPos.X;//任一分割路径的高度起始点均为有效字符出现的第一个点

            byte leftRightFlg = 0;//该标志位置位时，表示情况5与情况6可能会循环出现

            while ((iniPointX >= xIndex1) && (iniPointY < yIndex2))
            {
                Point newPos = new Point();
                newPos.Y = iniPointY;
                newPos.X = iniPointX;

                pathList.Add(newPos);


                if (((iniPointX - 1) <= 0) || ((iniPointY + 1) >= ImageWidth) || ((iniPointY - 1) <= 0))
                {
                    break;
                }

                Byte pointLeft = BinaryArray[iniPointX, (iniPointY - 1)];
                Byte pointRight = BinaryArray[iniPointX, (iniPointY + 1)];
                Byte pointDown = BinaryArray[(iniPointX - 1), iniPointY];
                Byte pointDownLeft = BinaryArray[(iniPointX - 1), (iniPointY - 1)];
                Byte pointDownRight = BinaryArray[(iniPointX - 1), (iniPointY + 1)];



                if (((0 == pointLeft) && (0 == pointRight) && (0 == pointDown) && (0 == pointDownLeft) && ((0 == pointDownRight)))// all black 11111
                     || ((255 == pointLeft) && (255 == pointRight) && (255 == pointDown) && (255 == pointDownLeft) && ((255 == pointDownRight))//all white 00000
                     || ((0 == pointDownLeft) && (255 == pointDown))))//情况1与情况3  //**10*
                {//down
                    iniPointX = iniPointX - 1;
                    leftRightFlg = 0;
                }
                else if (((255 == pointLeft) && (0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight))//0*011
                 || ((0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight) && (0 == pointLeft) && (0 == pointRight))//11011
                 || ((255 == pointLeft) && (0 == pointDown) && (255 == pointDownLeft) && (255 == pointDownRight)))//0*010

                {//left down //情况2
                    iniPointX = iniPointX - 1;
                    iniPointY = iniPointY - 1;

                    leftRightFlg = 0;
                }
                else if (((0 == pointDown) && (0 == pointDownLeft) && (255 == pointDownRight))//**110
                || ((0 == pointLeft) && (255 == pointRight) && (0 == pointDown) && (255 == pointDownLeft) && (255 == pointDownRight)))//10010
                {//情况4
                    iniPointX = iniPointX - 1;
                    iniPointY = iniPointY + 1;

                    leftRightFlg = 0;
                }
                else if (((255 == pointRight) && (0 == pointDown) && (0 == pointDownLeft) && (0 == pointDownRight))//*0111
                    || ((0 == pointLeft) && (255 == pointRight) && (0 == pointDown) && (255 == pointDownLeft) && (0 == pointDownRight)))//10011
                {//情况5
                    if (0 == leftRightFlg)
                    {
                        iniPointY = iniPointY + 1;

                        leftRightFlg = 1;

                    }
                    else//标志位为1时说明上一个点出现在情况6，而本次循环的点出现在情况5，此种情况将垂直渗透
                    {
                        iniPointX = iniPointX - 1;


                        leftRightFlg = 0;
                    }
                }
                else if ((255 == pointLeft) && (0 == pointDown) && (0 == pointDownLeft) && (0 == pointDownRight))//01111
                {//情况6
                    if (0 == leftRightFlg)
                    {
                        iniPointY = iniPointY - 1;

                        leftRightFlg = 1;
                    }
                    else//标志位为1时说明上一个点出现在情况5，而本次循环的点出现在情况6，此种情况将垂直渗透
                    {
                        iniPointX = iniPointX - 1;


                        leftRightFlg = 0;
                    }
                }
                else
                {
                    iniPointX = iniPointX - 1;
                }
            }

            pathList.Reverse();

            return pathList;
        }

    }
}
