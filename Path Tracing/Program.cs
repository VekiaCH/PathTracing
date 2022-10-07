using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Numerics;

namespace PathTracing
{
    class Program
    {
        static WriteableBitmap writeableBitmap;
        static Window w;
        static Image i;

        static Vector3 Eye = new Vector3(0, 0, -4);
        static Vector3 LookAt = new Vector3(0, 0, 6);
        static readonly double FOV = 36;
        static List<MySphere> spheres;
        static readonly Random random = new Random();

        static readonly int windowWidth = 512;
        static readonly int windowHeight = 512;

        [STAThread]
        static void Main(string[] args)
        {
            i = new Image();
            RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(i, EdgeMode.Aliased);

            w = new Window
            {
                Title = "Cornell Box | Path Tracing",
                Width = windowWidth,
                Height = windowHeight,
                Content = i
            };

            writeableBitmap = new WriteableBitmap(
                windowWidth,
                windowHeight,
                96,
                96,
                PixelFormats.Bgr32,
                null);

            i.Source = writeableBitmap;
            spheres = CreateListOfSpheres();
            Vector3 eyeRay;
            Vector3 color;
            int iterations = 1000;

            Console.WriteLine("Drawing image...");

            for (int k = 0; k < writeableBitmap.PixelHeight; k++)
            {
                for (int j = 0; j < writeableBitmap.PixelWidth; j++)
                {
                    color.X = 0;
                    color.Y = 0;
                    color.Z = 0;
                    eyeRay = CreateEyeRay(j - windowWidth / 2, k - windowHeight / 2);

                    for(int p = 0; p < iterations; p++)
                    {
                        color += ComputeColor(Eye, eyeRay);
                    }

                    color /= (float)iterations;
                    ColorPixel(j, k, color);
                }
            }

            Console.WriteLine("Done!");
            w.Show();

            Application app = new Application();
            app.Run();

        }

        private static Vector3 ComputeColor(Vector3 origin, Vector3 direction)
        {
            HitPoint hp = FindClosestHitPoint(spheres, origin, direction);
            Vector3 maxVector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

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
                    }
                }
            }
            return new HitPoint(h, direction, diffusion, emission, specular, sphereCenter);
        }

        private static Vector3 CreateEyeRay(int x, int y)
        {
            Vector3 Up = new Vector3(0, 1, 0);
            Vector3 r;
            Vector3 u;
            Vector3 f;
            f = LookAt - Eye;
            r = Vector3.Cross(Up, f);
            u = Vector3.Cross(f, r);
            Vector3 eyeRay;
            float tan = (float)Math.Tan((FOV / 2) * Math.PI / 180.0);
            eyeRay = Vector3.Normalize(f)
                + Vector3.Normalize(r) * (tan * x / (windowWidth / 2))
                + Vector3.Normalize(u) * (tan * -y / (windowHeight / 2));
            return eyeRay;
        }

        private static List<MySphere> CreateListOfSpheres()
        {
            List<MySphere> list = new List<MySphere>();
            list.Add(new MySphere(new Vector3(-1001, 0, 0), 1000, new Vector3(0.5f, 0, 0), new Vector3(0, 0, 0), new Vector3(0.5f, 0.5f, 0.5f))); //red
            list.Add(new MySphere(new Vector3(1001, 0, 0), 1000, new Vector3(0, 0, 0.5f), new Vector3(0, 0, 0), new Vector3(0.5f, 0.5f, 0.5f))); //blue
            list.Add(new MySphere(new Vector3(0, 0, 1001), 1000, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //gray
            list.Add(new MySphere(new Vector3(0, -1001, 0), 1000, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0, 0, 0))); //gray
            list.Add(new MySphere(new Vector3(0, 1001, 0), 1000, new Vector3(1, 1, 1), new Vector3(2, 2, 2), new Vector3(0, 0, 0))); //lightsource
            list.Add(new MySphere(new Vector3(-0.6f, -0.7f, -0.6f), 0.3, new Vector3(0.5f, 0.5f, 0), new Vector3(0, 0, 0), new Vector3(0.3f, 0.3f, 0.3f))); //yellow
            list.Add(new MySphere(new Vector3(0.3f, -0.4f, 0.3f), 0.6, new Vector3(0, 0.5f, 0.5f), new Vector3(0, 0, 0), new Vector3(0.8f, 0.8f, 0.8f))); //lightcyan
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
        public MySphere(Vector3 c, double r, Vector3 diffusion, Vector3 emission, Vector3 specular)
        {
            Center = c;
            Radius = r;
            Diffusion = diffusion;
            Emission = emission;
            Specular = specular;
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
