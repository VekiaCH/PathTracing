using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Numerics;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media.Effects;

namespace PathTracing
{
    class Program
    {
        static WriteableBitmap writeableBitmap;
        static Window w;
        static System.Windows.Controls.Image i;

        static Vector3 Eye = new Vector3(0, 0, -4);
        static Vector3 LookAt = new Vector3(0, 0, 6);
        static readonly double FOV = 36;
        static List<MySphere> spheres;
        static readonly Random random = new Random();

        static readonly int imageWidth = 512;
        static readonly int imageHeight = 512;

        static readonly Boolean AntiAliasing = true;
        static readonly Boolean Recursion = true;
        static readonly int CustomScene = 2;
        //iterations * 5 = rays per pixel on average
        static readonly int iterations = 10;

        [STAThread]
        static void Main(string[] args)
        {
            i = new System.Windows.Controls.Image();
            RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(i, EdgeMode.Aliased);

            w = new Window
            {
                Title = "Cornell Box | Anti-Aliasing",
                Width = imageWidth,
                Height = imageHeight + 31,
                Content = i
            };

            writeableBitmap = new WriteableBitmap(
                imageWidth,
                imageHeight,
                96,
                96,
                PixelFormats.Bgr32,
                null);

            i.Source = writeableBitmap;
            
            spheres = CreateListOfSpheres();
            Vector3 color;

            double percentDoneNow;
            double percentDonePrev = 0;

            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan ts;
            Console.WriteLine("Drawing image...");

            for (int k = 0; k < writeableBitmap.PixelHeight; k++)
            {
                for (int j = 0; j < writeableBitmap.PixelWidth; j++)
                {
                    color.X = 0;
                    color.Y = 0;
                    color.Z = 0;

                    if (Recursion)
                    {
                        for (int p = 0; p < iterations; p++)
                        {
                            color += ComputeColor(Eye, CreateEyeRay(j - imageWidth / 2, k - imageHeight / 2));
                        }

                        color /= (float)iterations;
                        ColorPixel(j, k, color);
                    } 
                    
                    else
                    {
                        ColorPixel(j, k, FindClosestHitPoint(spheres, Eye, CreateEyeRay(j - imageWidth / 2, k - imageHeight / 2)).Diffusion);
                    }
                }

                percentDoneNow = k / (double)writeableBitmap.PixelHeight * 100.0;
                if(Math.Round(percentDoneNow, 0) > Math.Round(percentDonePrev, 0) + 5)
                {
                    ts = stopwatch.Elapsed;
                    Console.WriteLine(Math.Round(percentDoneNow, 1) + "%");
                    Console.WriteLine("Elapsed Time is {0:00}:{1:00}:{2:00}.{3}",
                        ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
                    percentDonePrev = percentDoneNow;
                } 
            }
            Console.WriteLine("Done!");
            w.Show();

            Save();

            Application app = new Application();
            app.Run();
        }

        //saves the image to the source folder (only for IDE purposes)
        private static void Save()
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)i.ActualWidth, (int)i.ActualHeight, 96d, 96d, PixelFormats.Default);
            System.Windows.Size imageSize = new System.Windows.Size(i.ActualWidth, i.ActualHeight);
            i.Measure(imageSize);
            i.Arrange(new Rect(imageSize));
            rtb.Render(i);
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            
            using (var stream = File.Create("..//..//last_image.png"))
            {
                encoder.Save(stream);
            }
        }

        private static Vector3 ComputeColor(Vector3 origin, Vector3 direction)
        {
            HitPoint hp = FindClosestHitPoint(spheres, origin, direction);
            Vector3 maxVector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            //if no hitpoint was found it returns black
            if (hp.Point.Equals(maxVector))
            {
                return new Vector3(0,0,0);
            }

            else
            {
                int rand = random.Next(0, 100);
                if (rand < 20)
                {
                    return hp.Emission;
                }

                else
                {
                    Vector3 randomDirection = GenerateRandomDirection(hp);
                    return hp.Emission + ((float)(2 * Math.PI * Vector3.Dot(Vector3.Normalize(randomDirection), Vector3.Normalize(hp.Point - hp.SphereCenter)) / (1.0 - 0.2)) * BRDF(hp, randomDirection) * ComputeColor(hp.Point, randomDirection));
                }
            }
           
        }

        private static Vector3 GenerateRandomDirection(HitPoint hp)
        {
            Vector3 direction = new Vector3(2, 2, 2);
            Vector3 sphereNormal = hp.Point - hp.SphereCenter;

            while(direction.Length() > 1)
            {
                direction.X = (float)(random.NextDouble() * 2.0 - 1.0);
                direction.Y = (float)(random.NextDouble() * 2.0 - 1.0);
                direction.Z = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            if(Vector3.Dot(direction, sphereNormal) < 0)
            {
                direction *= -1;
            }
            
            return direction;
        }
        
        private static Vector3 BRDF(HitPoint hp, Vector3 randomDirection)
        {
            Vector3 reflectionHP = CalculateReflection(hp);
            if(Vector3.Dot(Vector3.Normalize(reflectionHP), Vector3.Normalize(randomDirection)) > 1.0f - 0.01f)
            {
                return (hp.Diffusion + 10 * hp.Specular) * (float)(1.0 / Math.PI);
            }
            else
            {
                return hp.Diffusion * (float)(1.0 / Math.PI);
            }
        }

        private static Vector3 CalculateReflection(HitPoint hp)
        {
            Vector3 sphereNormal = Vector3.Normalize(hp.Point - hp.SphereCenter);
            Vector3 normHP = hp.Direction;
            return normHP - 2 * Vector3.Dot(normHP, sphereNormal) * sphereNormal;
        }

        private static void ColorPixel(int j, int k, Vector3 color)
        {
            color.X = color.X > 1 ? 1 : color.X;
            color.Y = color.Y > 1 ? 1 : color.Y;
            color.Z = color.Z > 1 ? 1 : color.Z;
            byte[] color_data = { (byte)(Math.Pow(color.Z, 1.0/2.2) * 255), (byte)(Math.Pow(color.Y, 1.0 / 2.2) * 255), (byte)(Math.Pow(color.X, 1.0 / 2.2) * 255), 255 };
            int stride = writeableBitmap.PixelWidth * writeableBitmap.Format.BitsPerPixel / 8;
            writeableBitmap.WritePixels(new Int32Rect(j, k, 1, 1), color_data, stride, 0);
        }

        private static HitPoint FindClosestHitPoint(List<MySphere> spheres, Vector3 origin, Vector3 direction)
        {
            Vector3 h = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 emission = new Vector3();
            Vector3 diffusion = new Vector3();
            Vector3 specular = new Vector3();
            Vector3 sphereCenter = new Vector3();

            float lambda_preview;
            float lambda_one;
            float lambda_two;
            float lambda = float.MaxValue;
            float b;
            float c;

            Vector3 CE;
            float CELen;

            foreach (MySphere sphere in spheres)
            {
                CE = origin - sphere.Center;
                CELen = CE.LengthSquared();
                b = Vector3.Dot(CE, 2 * Vector3.Normalize(direction));
                c = (float)(CELen - sphere.Radius * sphere.Radius);
                
                if (b * b >= 4 * c)
                {
                    lambda_one = (float)((-b + Math.Sqrt(b * b - 4 * c)) / 2);
                    lambda_two = (float)((-b - Math.Sqrt(b * b - 4 * c)) / 2);
                    if (lambda_one < lambda_two)
                    {
                        lambda_preview = lambda_one;
                    }
                    else
                    {
                        lambda_preview = lambda_two;
                    }

                    if (lambda_preview < lambda && lambda_preview >= 0)
                    {
                        lambda = lambda_preview;
                        diffusion = sphere.Diffusion;
                        emission = sphere.Emission;
                        specular = sphere.Specular;
                        sphereCenter = sphere.Center;
                        h = origin + lambda * Vector3.Normalize(direction);
                        if (sphere.bitmap != null)
                        {
                            Vector3 sphereNormal = Vector3.Normalize(h - sphere.Center);
                            int x = (int)((sphere.bitmap.Width - 1) * ((Math.Atan2(sphereNormal.Z, sphereNormal.X) + Math.PI) / (2 * Math.PI)));
                            int y = (int)((sphere.bitmap.Height - 1) * (Math.Acos(sphereNormal.Y) / Math.PI));
                            System.Drawing.Color color = sphere.bitmap.GetPixel(x, y);
                            diffusion.X = (float)Math.Pow(color.R / 255f, 2.2f);
                            diffusion.Y = (float)Math.Pow(color.G / 255f, 2.2f);
                            diffusion.Z = (float)Math.Pow(color.B / 255f, 2.2f);
                        }
                    }
                }
            }

            return new HitPoint(h, direction, diffusion, emission, specular, sphereCenter);
        }

        private static Vector3 CreateEyeRay(float x, float y)
        {
            Vector3 Up = new Vector3(0, 1, 0);
            Vector3 r;
            Vector3 u;
            Vector3 f;
            f = LookAt - Eye;
            r = Vector3.Cross(Up, f);
            u = Vector3.Cross(f, r);
            if (AntiAliasing)
            {
                x += (float)randomGaussian();
                y += (float)randomGaussian();
            }
            Vector3 eyeRay;
            float tan = (float)Math.Tan((FOV / 2) * Math.PI / 180.0);
            eyeRay = Vector3.Normalize(f)
                + Vector3.Normalize(r) * (tan * x / (imageWidth / 2f))
                + Vector3.Normalize(u) * (tan * -y / (imageHeight / 2f));
            return eyeRay;
        }

        private static double randomGaussian()
        {
            double x1 = 1.0 - random.NextDouble();
            double x2 = 1.0 - random.NextDouble();
            double randomNormal = Math.Sqrt(-2.0 * Math.Log(x1)) * Math.Sin(2.0 * Math.PI * x2);
            double sigma = 0.5;
            return randomNormal * sigma;
        }

        private static List<MySphere> CreateListOfSpheres()
        {
            List<MySphere> list = new List<MySphere>();
            if(CustomScene == 1)
            {
                list.Add(new MySphere(new Vector3(-101, 0, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //left wall
                list.Add(new MySphere(new Vector3(101, 0, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //right wall
                list.Add(new MySphere(new Vector3(0, 0, 101), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); // back wall
                list.Add(new MySphere(new Vector3(0, -101, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //bottom wall
                list.Add(new MySphere(new Vector3(0, 101, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //top wall
                list.Add(new MySphere(new Vector3(0, 0, -101), 100, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(2, 2, 2), new Vector3(0, 0, 0))); //front wall
                list.Add(new MySphere(new Vector3(0, 0, 0), 0.9, new Vector3(0, 0, 0), new Vector3(0.1f, 0.1f, 0), new Vector3(0, 0, 0), new Bitmap("ore_texture.jpg"))); //glowing uranium sphere
            }
            else if (CustomScene == 2)
            {
                list.Add(new MySphere(new Vector3(40, 20, 150), 5, new Vector3(0.7f, 0.7f, 0.7f), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Bitmap("2k_mercury.jpg"))); //moon
                list.Add(new MySphere(new Vector3(-20, -20, 120), 10, new Vector3(0.3f, 0.3f, 0.7f), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Bitmap("2k_jupiter.jpg"))); //blue moon
                list.Add(new MySphere(new Vector3(0, 0, 200), 50, new Vector3(1, 0.5f, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Bitmap("2k_earth_daymap.jpg"))); //earth
                list.Add(new MySphere(new Vector3(1300, 0, 200), 1000, new Vector3(1, 0.5f, 0), new Vector3(2, 1.7f, 1.5f), new Vector3(0, 0, 0))); //sun
            }
            else if(CustomScene == 3)
            {
                list.Add(new MySphere(new Vector3(-101, 0, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //left wall
                list.Add(new MySphere(new Vector3(101, 0, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //right wall
                list.Add(new MySphere(new Vector3(0, -101, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //bottom wall
                list.Add(new MySphere(new Vector3(0, 101, 0), 100, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //top wall
                list.Add(new MySphere(new Vector3(0, 1.95f, 0), 1.1, new Vector3(1, 1, 1), new Vector3(2, 2, 2), new Vector3(0, 0, 0))); //lightsource
                list.Add(new MySphere(new Vector3(0, 0, 0), 0.5, new Vector3(0.5f, 0, 0), new Vector3(0, 0, 0), new Vector3(1, 0, 0))); //oxygen ball
                list.Add(new MySphere(new Vector3(0.394f, 0.307f, 0), 0.3, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(1, 1, 1))); //hydrogen ball
                list.Add(new MySphere(new Vector3(-0.394f, 0.307f, 0), 0.3, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(1, 1, 1))); //hydrogen ball
            }
            else
            {
                list.Add(new MySphere(new Vector3(-1001, 0, 0), 1000, new Vector3(0.5f, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //red
                list.Add(new MySphere(new Vector3(1001, 0, 0), 1000, new Vector3(0, 0, 0.5f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //blue
                list.Add(new MySphere(new Vector3(0, 0, 1001), 1000, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //gray
                list.Add(new MySphere(new Vector3(0, -1001, 0), 1000, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //gray
                list.Add(new MySphere(new Vector3(0, 1001, 0), 1000, new Vector3(1, 1, 1), new Vector3(2, 2, 2), new Vector3(0, 0, 0))); //lightsource
                list.Add(new MySphere(new Vector3(-0.6f, -0.7f, -0.6f), 0.3, new Vector3(0.5f, 0.5f, 0), new Vector3(0, 0, 0), new Vector3(0.4f, 0.4f, 0.4f))); //yellow
                list.Add(new MySphere(new Vector3(0.3f, -0.4f, 0.3f), 0.6, new Vector3(0, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0.4f, 0.4f, 0.4f))); //lightcyan
            }
            
            return list;
        }
    }

    class MySphere
    {
        public Vector3 Center { get; set; }
        public double Radius { get; set; }
        public Vector3 Diffusion { get; set; }
        public Vector3 Emission { get; set; }
        public Vector3 Specular { get; set; }
        public Bitmap bitmap { get; set; }

        public MySphere(Vector3 c, double r, Vector3 diffusion, Vector3 emission, Vector3 specular)
        {
            Center = c;
            Radius = r;
            Diffusion = diffusion;
            Emission = emission;
            Specular = specular;
            bitmap = null;
        }

        public MySphere(Vector3 c, double r, Vector3 diffusion, Vector3 emission, Vector3 specular, Bitmap bmp)
        {
            Center = c;
            Radius = r;
            Diffusion = diffusion;
            Emission = emission;
            Specular = specular;
            bitmap = bmp;
        }
    }

    class HitPoint
    {
        public Vector3 Point { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Diffusion { get; set; }
        public Vector3 Emission { get; set; }
        public Vector3 Specular { get; set; }
        public Vector3 SphereCenter { get; set; } 
        public HitPoint(Vector3 p, Vector3 direction, Vector3 d, Vector3 e, Vector3 s, Vector3 sphereCenter)
        {
            Point = p;
            Direction = direction;
            Diffusion = d;
            Emission = e;
            Specular = s;
            SphereCenter = sphereCenter;
        }
    }
}
