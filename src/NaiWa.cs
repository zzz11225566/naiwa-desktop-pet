using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        App app = new App();
        app.Run();
    }
}

public class App : Application
{
    private PetWindow _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _window = new PetWindow();
        _window.Show();
    }
}

public class PetSettings
{
    public double Scale = 1.0;
    public bool Topmost = true;
    public double X = double.NaN;
    public double Y = double.NaN;

    private static string FilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NaiWa");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static PetSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return Parse(json);
            }
        }
        catch
        {
        }
        return new PetSettings();
    }

    public void Save()
    {
        try
        {
            string json =
                "{\"scale\":" + Scale.ToString("0.###", CultureInfo.InvariantCulture) +
                ",\"topmost\":" + (Topmost ? "true" : "false") +
                ",\"x\":" + (double.IsNaN(X) ? "null" : X.ToString("0.#", CultureInfo.InvariantCulture)) +
                ",\"y\":" + (double.IsNaN(Y) ? "null" : Y.ToString("0.#", CultureInfo.InvariantCulture)) +
                "}";
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    private static PetSettings Parse(string json)
    {
        PetSettings s = new PetSettings();
        json = json.Trim().Trim('{', '}');
        foreach (string part in SplitJson(json))
        {
            string[] kv = part.Split(new char[] { ':' }, 2);
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().Trim('"').ToLowerInvariant();
            string val = kv[1].Trim().Trim('"').ToLowerInvariant();
            double d;
            if (key == "scale" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.Scale = d;
            else if (key == "topmost")
                s.Topmost = val != "false";
            else if (key == "x" && val != "null" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.X = d;
            else if (key == "y" && val != "null" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                s.Y = d;
        }
        return s;
    }

    private static IEnumerable<string> SplitJson(string body)
    {
        var list = new List<string>();
        int start = 0;
        bool inString = false;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '"') inString = !inString;
            if (c == ',' && !inString)
            {
                list.Add(body.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (start < body.Length) list.Add(body.Substring(start));
        return list;
    }
}

public struct Pose
{
    public double RootY;
    public double SquashY;
    public double SquashXZ;
    public double BodyYaw;
    public double BodyPitch;
    public double BodyRoll;
    public double HeadPitch;
    public double HeadTilt;
    public double HeadLookYaw;
    public double ArmL;
    public double ArmR;
    public double HandL;
    public double HandR;
    public double LegL;
    public double LegR;
    public double Tongue;
    public double Blink;
    public double Mouth;

    public static Pose Lerp(Pose a, Pose b, double t, double yawFrom)
    {
        Pose r = new Pose();
        r.RootY = Lerp(a.RootY, b.RootY, t);
        r.SquashY = Lerp(a.SquashY, b.SquashY, t);
        r.SquashXZ = Lerp(a.SquashXZ, b.SquashXZ, t);
        r.BodyYaw = LerpAngle(yawFrom, b.BodyYaw, t);
        r.BodyPitch = Lerp(a.BodyPitch, b.BodyPitch, t);
        r.BodyRoll = Lerp(a.BodyRoll, b.BodyRoll, t);
        r.HeadPitch = Lerp(a.HeadPitch, b.HeadPitch, t);
        r.HeadTilt = Lerp(a.HeadTilt, b.HeadTilt, t);
        r.HeadLookYaw = Lerp(a.HeadLookYaw, b.HeadLookYaw, t);
        r.ArmL = Lerp(a.ArmL, b.ArmL, t);
        r.ArmR = Lerp(a.ArmR, b.ArmR, t);
        r.HandL = Lerp(a.HandL, b.HandL, t);
        r.HandR = Lerp(a.HandR, b.HandR, t);
        r.LegL = Lerp(a.LegL, b.LegL, t);
        r.LegR = Lerp(a.LegR, b.LegR, t);
        r.Tongue = Lerp(a.Tongue, b.Tongue, t);
        r.Blink = Lerp(a.Blink, b.Blink, t);
        r.Mouth = Lerp(a.Mouth, b.Mouth, t);
        return r;
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private static double LerpAngle(double a, double b, double t)
    {
        double d = b - a;
        while (d > Math.PI) d -= Math.PI * 2;
        while (d < -Math.PI) d += Math.PI * 2;
        return a + d * t;
    }
}

public class PetModel
{
    private readonly Random _rng = new Random();
    public readonly ScaleTransform3D GlobalScale = new ScaleTransform3D(1, 1, 1);
    private readonly Part _root;
    private readonly Part _body;
    private readonly Part _belly;
    private readonly Part _head;
    private readonly Part _armL;
    private readonly Part _armR;
    private readonly Part _handL;
    private readonly Part _handR;
    private readonly Part _legL;
    private readonly Part _legR;
    private readonly Part _footL;
    private readonly Part _footR;
    private readonly Part _eyeL;
    private readonly Part _eyeR;
    private readonly Part _pupilL;
    private readonly Part _pupilR;
    private readonly Part _tongue;
    private readonly Part _mouth;
    private readonly Part _cheekL;
    private readonly Part _cheekR;

    private double _time;
    private double _baseYaw;
    private double _bodyYawCurrent;
    private double _nextYawAt = double.PositiveInfinity;
    private double _nextBlinkAt;
    private string _action;
    private double _actionStart;
    private double _actionIn = 0.32;
    private double _actionOut = 0.48;
    private double _holdDuration;

    public PetModel()
    {
        _root = new Part();
        _body = new Part();
        _belly = new Part();
        _head = new Part();
        _armL = new Part();
        _armR = new Part();
        _handL = new Part();
        _handR = new Part();
        _legL = new Part();
        _legR = new Part();
        _footL = new Part();
        _footR = new Part();
        _eyeL = new Part();
        _eyeR = new Part();
        _pupilL = new Part();
        _pupilR = new Part();
        _tongue = new Part();
        _mouth = new Part();
        _cheekL = new Part();
        _cheekR = new Part();
    }

    public void Build(Model3DGroup world)
    {
        var cream = Color.FromRgb(246, 228, 192);
        var creamDark = Color.FromRgb(222, 196, 148);
        var bellyColor = Color.FromRgb(250, 242, 222);
        var spot = Color.FromRgb(91, 58, 33);
        var foot = Color.FromRgb(226, 185, 122);
        var cheek = Color.FromRgb(238, 142, 138);
        var white = Color.FromRgb(250, 250, 248);
        var pupil = Color.FromRgb(28, 22, 22);
        var mouth = Color.FromRgb(98, 57, 44);
        var tongue = Color.FromRgb(232, 113, 108);

        var rootGroup = new Model3DGroup { Transform = _root.Group };
        var bodyGroup = new Model3DGroup { Transform = _body.Group };
        bodyGroup.Children.Add(Sphere(new Point3D(0, 0, 0), 1.08, 1.02, 0.88, cream));
        bodyGroup.Children.Add(Sphere(new Point3D(0.02, -0.12, 0.58), 0.72, 0.68, 0.36, bellyColor));
        bodyGroup.Children.Add(Spot(new Point3D(-0.34, 0.62, -0.62), 0.18, spot));
        bodyGroup.Children.Add(Spot(new Point3D(0.26, 0.42, -0.72), 0.15, spot));
        bodyGroup.Children.Add(Spot(new Point3D(-0.12, -0.18, -0.82), 0.20, spot));
        bodyGroup.Children.Add(Spot(new Point3D(0.52, -0.10, -0.64), 0.16, spot));
        bodyGroup.Children.Add(Spot(new Point3D(-0.48, 0.08, -0.58), 0.14, spot));
        bodyGroup.Children.Add(Spot(new Point3D(0.04, 0.20, -0.84), 0.13, spot));
        rootGroup.Children.Add(bodyGroup);

        var headGroup = new Model3DGroup { Transform = _head.Group };
        headGroup.Children.Add(Sphere(new Point3D(0, 0, 0), 0.78, 0.70, 0.70, cream));
        headGroup.Children.Add(Sphere(new Point3D(0, 0.34, 0.10), 0.64, 0.46, 0.48, creamDark));
        headGroup.Children.Add(Sphere(new Point3D(0, -0.06, 0.48), 0.52, 0.32, 0.22, bellyColor));

        var eyeL = new Model3DGroup { Transform = _eyeL.Group };
        eyeL.Children.Add(Sphere(new Point3D(0, 0, 0), 0.235, 0.245, 0.20, white));
        var pupilL = new Model3DGroup { Transform = _pupilL.Group };
        pupilL.Children.Add(Sphere(new Point3D(0, 0, 0), 0.115, 0.125, 0.10, pupil));
        pupilL.Children.Add(Sphere(new Point3D(0.025, 0.035, 0.105), 0.045, 0.045, 0.025, Color.FromRgb(245, 245, 242)));
        eyeL.Children.Add(pupilL);

        var eyeR = new Model3DGroup { Transform = _eyeR.Group };
        eyeR.Children.Add(Sphere(new Point3D(0, 0, 0), 0.235, 0.245, 0.20, white));
        var pupilR = new Model3DGroup { Transform = _pupilR.Group };
        pupilR.Children.Add(Sphere(new Point3D(0, 0, 0), 0.115, 0.125, 0.10, pupil));
        pupilR.Children.Add(Sphere(new Point3D(0.025, 0.035, 0.105), 0.045, 0.045, 0.025, Color.FromRgb(245, 245, 242)));
        eyeR.Children.Add(pupilR);

        headGroup.Children.Add(eyeL);
        headGroup.Children.Add(eyeR);
        headGroup.Children.Add(Cheek(new Point3D(0, 0, 0), cheek, _cheekL));
        headGroup.Children.Add(Cheek(new Point3D(0, 0, 0), cheek, _cheekR));
        headGroup.Children.Add(new Model3DGroup { Transform = _mouth.Group, Children = { Sphere(new Point3D(0, 0, 0), 0.18, 0.075, 0.10, mouth) } });
        headGroup.Children.Add(new Model3DGroup { Transform = _tongue.Group, Children = { Sphere(new Point3D(0, 0, 0), 0.11, 0.13, 0.62, tongue) } });
        rootGroup.Children.Add(headGroup);

        rootGroup.Children.Add(Limb(new Point3D(0, 0, 0), creamDark, _armL));
        rootGroup.Children.Add(Limb(new Point3D(0, 0, 0), creamDark, _armR));
        rootGroup.Children.Add(Hand(new Point3D(0, 0, 0), foot, _handL));
        rootGroup.Children.Add(Hand(new Point3D(0, 0, 0), foot, _handR));
        rootGroup.Children.Add(Leg(new Point3D(0, 0, 0), creamDark, _legL));
        rootGroup.Children.Add(Leg(new Point3D(0, 0, 0), creamDark, _legR));
        rootGroup.Children.Add(Foot(new Point3D(0, 0, 0), foot, _footL));
        rootGroup.Children.Add(Foot(new Point3D(0, 0, 0), foot, _footR));

        var globalGroup = new Model3DGroup { Transform = GlobalScale };
        globalGroup.Children.Add(rootGroup);
        world.Children.Add(globalGroup);

        _baseYaw = PickYaw();
        _bodyYawCurrent = _baseYaw;
        _nextYawAt = _time + NextIdleDelay();
        _nextBlinkAt = _time + 1.2 + _rng.NextDouble() * 2.5;
    }

    private double PickYaw()
    {
        int n = _rng.Next(4);
        return n * Math.PI / 2.0;
    }

    private double NextIdleDelay()
    {
        return 6.0 + _rng.NextDouble() * 8.0;
    }

    public void TriggerAction(string action, double hold)
    {
        _action = action;
        _actionStart = _time;
        _holdDuration = Math.Max(1.8, hold);
    }

    public void Update(double dt, double time)
    {
        _time = time;
        if (double.IsPositiveInfinity(_nextYawAt))
        {
            _nextYawAt = _time + NextIdleDelay();
        }
        if (_time >= _nextYawAt)
        {
            _baseYaw = PickYaw();
            _nextYawAt = _time + NextIdleDelay();
        }
        double yawDelta = _baseYaw - _bodyYawCurrent;
        while (yawDelta > Math.PI) yawDelta -= Math.PI * 2;
        while (yawDelta < -Math.PI) yawDelta += Math.PI * 2;
        _bodyYawCurrent += yawDelta * Math.Min(1.0, dt * 2.2);
        if (_time >= _nextBlinkAt)
        {
            _nextBlinkAt = _time + 2.4 + _rng.NextDouble() * 3.6;
        }

        Pose idle = IdlePose();
        Pose pose;
        if (_action != null)
        {
            double elapsed = _time - _actionStart;
            double end = _actionIn + _holdDuration + _actionOut;
            double p = elapsed / _actionIn;
            double q = (elapsed - _actionIn - _holdDuration) / _actionOut;
            double ease = Clamp01(p) * (1.0 - Clamp01(q));
            ease = SmoothStep(Clamp01(ease));
            Pose peak = ActionPeak(_action);
            pose = Pose.Lerp(idle, peak, ease, idle.BodyYaw);
            if (elapsed >= end)
            {
                _action = null;
            }
        }
        else
        {
            pose = idle;
        }

        ApplyPose(pose);
    }

    private Pose IdlePose()
    {
        Pose p = new Pose();
        double breathe = Math.Sin(_time * 2.1);
        double bob = Math.Sin(_time * 1.7);
        p.RootY = 0.00 + 0.025 * bob;
        p.SquashY = 1.0 + 0.025 * breathe;
        p.SquashXZ = 1.0 - 0.016 * breathe;
        p.BodyYaw = _bodyYawCurrent + 0.07 * Math.Sin(_time * 0.6);
        p.BodyPitch = 0.015 * Math.Sin(_time * 1.3);
        p.BodyRoll = 0.025 * Math.Sin(_time * 0.8);
        p.HeadPitch = -0.05 + 0.04 * Math.Sin(_time * 1.9);
        p.HeadTilt = 0.10 * Math.Sin(_time * 0.7);
        p.HeadLookYaw = 0.0;
        p.ArmL = -0.08 + 0.05 * Math.Sin(_time * 1.1);
        p.ArmR = -0.08 - 0.05 * Math.Sin(_time * 1.1);
        p.HandL = 0.15;
        p.HandR = 0.15;
        p.LegL = 0.30;
        p.LegR = 0.30;
        p.Tongue = 0.0;
        p.Blink = BlinkAmount();
        p.Mouth = 0.08 + 0.025 * Math.Sin(_time * 1.4);
        return p;
    }

    private double BlinkAmount()
    {
        double dt = _time - _nextBlinkAt;
        if (dt >= 0 && dt < 0.12)
        {
            return SmoothStep(dt / 0.12);
        }
        if (dt >= 0.12 && dt < 0.22)
        {
            return 1.0 - SmoothStep((dt - 0.12) / 0.10);
        }
        return 0.0;
    }

    private Pose ActionPeak(string action)
    {
        Pose p = IdlePose();
        switch (action)
        {
            case "bounce":
                p.RootY = 0.46;
                p.SquashY = 0.88;
                p.SquashXZ = 1.10;
                p.HeadPitch = -0.10;
                p.ArmL = 0.35;
                p.ArmR = 0.35;
                p.HandL = -0.20;
                p.HandR = -0.20;
                p.LegL = 0.10;
                p.LegR = 0.10;
                break;
            case "wave":
                p.ArmR = 1.65;
                p.HandR = -0.55;
                p.HeadTilt = 0.18;
                p.Mouth = 0.30;
                break;
            case "peek":
                p.BodyPitch = -0.24;
                p.RootY = 0.12;
                p.HeadPitch = 0.16;
                p.ArmL = 0.25;
                p.ArmR = 0.25;
                p.Mouth = 0.16;
                break;
            case "stretch":
                p.RootY = 0.10;
                p.SquashY = 1.16;
                p.SquashXZ = 0.94;
                p.HeadPitch = -0.18;
                p.ArmL = 1.75;
                p.ArmR = 1.75;
                p.HandL = -0.25;
                p.HandR = -0.25;
                p.LegL = 0.42;
                p.LegR = 0.42;
                break;
            case "tongue":
                p.HeadPitch = 0.08;
                p.Tongue = 1.0;
                p.Mouth = 0.55;
                p.ArmL = 0.08;
                p.ArmR = 0.08;
                break;
            case "blink":
                p.Blink = 1.0;
                p.Mouth = 0.24;
                break;
            case "spin":
                p.BodyYaw = _baseYaw + Math.PI * 2.0;
                p.ArmL = 0.55;
                p.ArmR = 0.55;
                p.HandL = -0.30;
                p.HandR = -0.30;
                p.HeadTilt = 0.12;
                break;
        }
        return p;
    }

    private void ApplyPose(Pose p)
    {
        _root.Set(
            0, p.RootY, 0,
            p.BodyPitch, p.BodyYaw, p.BodyRoll,
            p.SquashXZ, p.SquashY, p.SquashXZ);

        _body.Set(0, 0, 0, 0, 0, 0, 1, 1, 1);
        _belly.Set(0.02, -0.12, 0.58, 0, 0, 0, 1, 1, 1);
        _head.Set(
            0, 1.12, 0.16,
            p.HeadPitch, -p.BodyYaw + p.HeadLookYaw, p.HeadTilt,
            1, 1, 1);

        _eyeL.Set(-0.36, 1.16, 0.46, 0, 0, 0, 1, 1 - 0.90 * p.Blink, 1);
        _eyeR.Set(0.36, 1.16, 0.46, 0, 0, 0, 1, 1 - 0.90 * p.Blink, 1);
        _pupilL.Set(0, 0, 0.12, 0, 0, 0, 1, 1, 1);
        _pupilR.Set(0, 0, 0.12, 0, 0, 0, 1, 1, 1);

        _cheekL.Set(-0.55, 0.93, 0.38, 0, 0, 0, 0.18, 0.11, 0.08);
        _cheekR.Set(0.55, 0.93, 0.38, 0, 0, 0, 0.18, 0.11, 0.08);
        _mouth.Set(0, 0.84, 0.55, 0, 0, 0, 1, 1, p.Mouth);
        _tongue.Set(0, 0.78, 0.66, 0, 0, 0, 1, 1, 0.06 + p.Tongue);

        _armL.Set(-0.88, -0.12, 0.18, 0, p.ArmL, 0.30, 0.42, 1.0, 0.42);
        _armR.Set(0.88, -0.12, 0.18, 0, -p.ArmR, -0.30, 0.42, 1.0, 0.42);
        _handL.Set(-0.94, -0.48, 0.42, p.HandL, 0, 0, 0.34, 0.28, 0.34);
        _handR.Set(0.94, -0.48, 0.42, -p.HandR, 0, 0, 0.34, 0.28, 0.34);
        _legL.Set(-0.50, -0.78, 0.18, p.LegL, 0, 0.30, 0.40, 0.90, 0.40);
        _legR.Set(0.50, -0.78, 0.18, -p.LegR, 0, -0.30, 0.40, 0.90, 0.40);
        _footL.Set(-0.42, -1.10, 0.48, 0, 0, 0, 0.46, 0.24, 0.58);
        _footR.Set(0.42, -1.10, 0.48, 0, 0, 0, 0.46, 0.24, 0.58);
    }

    private static Model3DGroup Sphere(Point3D center, double rx, double ry, double rz, Color color)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(rx, ry, rz),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                },
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(rx, ry, rz),
                    Material = new SpecularMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 250, 235)), 42)
                }
            },
            Transform = new TranslateTransform3D(center.X, center.Y, center.Z)
        };
    }

    private static Model3DGroup Spot(Point3D center, double r, Color color)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(r, r * 0.55, r * 0.18),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = new TranslateTransform3D(center.X, center.Y, center.Z)
        };
    }

    private static Model3DGroup Cheek(Point3D center, Color color, Part part)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(1, 1, 1),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = part.Group
        };
    }

    private static Model3DGroup Limb(Point3D center, Color color, Part part)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(1, 1, 1),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = part.Group
        };
    }

    private static Model3DGroup Hand(Point3D center, Color color, Part part)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(1, 1, 1),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = part.Group
        };
    }

    private static Model3DGroup Leg(Point3D center, Color color, Part part)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(1, 1, 1),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = part.Group
        };
    }

    private static Model3DGroup Foot(Point3D center, Color color, Part part)
    {
        return new Model3DGroup
        {
            Children =
            {
                new GeometryModel3D
                {
                    Geometry = MakeEllipsoid(1, 1, 1),
                    Material = new DiffuseMaterial(new SolidColorBrush(color)),
                    BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
                }
            },
            Transform = part.Group
        };
    }

    private static MeshGeometry3D MakeEllipsoid(double rx, double ry, double rz)
    {
        int stacks = 16;
        int slices = 24;
        var mesh = new MeshGeometry3D();
        var pts = new Point3DCollection();
        var nrm = new Vector3DCollection();
        var idx = new Int32Collection();
        for (int i = 0; i <= stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            for (int j = 0; j <= slices; j++)
            {
                double theta = Math.PI * 2 * j / slices;
                double x = rx * sinPhi * Math.Cos(theta);
                double y = ry * cosPhi;
                double z = rz * sinPhi * Math.Sin(theta);
                pts.Add(new Point3D(x, y, z));
                double nx = x / (rx * rx);
                double ny = y / (ry * ry);
                double nz = z / (rz * rz);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 0.0001) len = 1;
                nrm.Add(new Vector3D(nx / len, ny / len, nz / len));
            }
        }
        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                int a = i * (slices + 1) + j;
                int b = a + slices + 1;
                idx.Add(a);
                idx.Add(b);
                idx.Add(a + 1);
                idx.Add(a + 1);
                idx.Add(b);
                idx.Add(b + 1);
            }
        }
        mesh.Positions = pts;
        mesh.Normals = nrm;
        mesh.TriangleIndices = idx;
        return mesh;
    }

    private static double Clamp01(double x)
    {
        if (x < 0) return 0;
        if (x > 1) return 1;
        return x;
    }

    private static double SmoothStep(double x)
    {
        x = Clamp01(x);
        return x * x * (3 - 2 * x);
    }
}

public class Part
{
    public readonly Transform3DGroup Group;
    private readonly ScaleTransform3D _scale;
    private readonly RotateTransform3D _rotX;
    private readonly RotateTransform3D _rotY;
    private readonly RotateTransform3D _rotZ;
    private readonly TranslateTransform3D _translate;

    public Part()
    {
        _scale = new ScaleTransform3D(1, 1, 1);
        _rotX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0));
        _rotY = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0));
        _rotZ = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0));
        _translate = new TranslateTransform3D(0, 0, 0);
        Group = new Transform3DGroup();
        Group.Children.Add(_scale);
        Group.Children.Add(_rotX);
        Group.Children.Add(_rotY);
        Group.Children.Add(_rotZ);
        Group.Children.Add(_translate);
    }

    public void Set(double px, double py, double pz, double rx, double ry, double rz, double sx, double sy, double sz)
    {
        _translate.OffsetX = px;
        _translate.OffsetY = py;
        _translate.OffsetZ = pz;
        ((AxisAngleRotation3D)_rotX.Rotation).Angle = rx * 180.0 / Math.PI;
        ((AxisAngleRotation3D)_rotY.Rotation).Angle = ry * 180.0 / Math.PI;
        ((AxisAngleRotation3D)_rotZ.Rotation).Angle = rz * 180.0 / Math.PI;
        _scale.ScaleX = sx;
        _scale.ScaleY = sy;
        _scale.ScaleZ = sz;
    }
}

public class PetWindow : Window
{
    private const double BaseSize = 330.0;

    private readonly PetModel _model;
    private readonly Viewport3D _viewport;
    private readonly PetSettings _settings;
    private readonly Random _rng = new Random();
    private DateTime _lastFrame = DateTime.UtcNow;
    private DateTime _downTime;
    private Point _downPoint;
    private Point _downScreen;
    private bool _mouseDown;
    private bool _dragging;
    private bool _suppressAction;
    private PanelWindow _panel;
    private readonly DispatcherTimer _clickTimer;

    private static readonly string[] Actions =
    {
        "bounce", "wave", "peek", "stretch", "tongue", "blink", "spin"
    };

    public PetWindow()
    {
        _settings = PetSettings.Load();
        _model = new PetModel();
        _clickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(420)
        };
        _clickTimer.Tick += OnClickTimerTick;
        _viewport = new Viewport3D
        {
            Camera = new PerspectiveCamera(new Point3D(0, 0.05, 7.4), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 36)
        };

        var world = new Model3DGroup();
        _model.Build(world);
        _viewport.Children.Add(new ModelVisual3D { Content = world });
        _viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Color.FromArgb(210, 255, 252, 244)) });
        _viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Color.FromArgb(235, 255, 248, 235), new Vector3D(-0.45, -0.75, -0.85)) });
        _viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Color.FromArgb(90, 180, 205, 235), new Vector3D(0.45, 0.30, 0.70)) });

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = _settings.Topmost;
        Content = _viewport;

        ApplyScale(_settings.Scale * BaseSize);

        var work = SystemParameters.WorkArea;
        double x = _settings.X;
        double y = _settings.Y;
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            x = work.Right - Width - 26;
            y = work.Bottom - Height - 28;
        }
        Left = Clamp(x, work.Left, work.Right - Width);
        Top = Clamp(y, work.Top, work.Bottom - Height);

        MouseLeftButtonDown += OnMouseLeftDown;
        MouseLeftButtonUp += OnMouseLeftUp;
        MouseMove += OnMouseMove;
        MouseRightButtonUp += OnRightClick;
        CompositionTarget.Rendering += OnRendering;
        Closing += OnClosing;
    }

    private void OnRendering(object sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double dt = Math.Min(0.05, (now - _lastFrame).TotalSeconds);
        _lastFrame = now;
        double t = now.TimeOfDay.TotalSeconds;
        _model.Update(dt, t);
    }

    private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = true;
        _dragging = false;
        _downTime = DateTime.UtcNow;
        _downPoint = e.GetPosition(this);
        _downScreen = PointToScreen(e.GetPosition(this));
        Mouse.Capture(this);

        if (e.ClickCount == 2)
        {
            _mouseDown = false;
            _dragging = false;
            _clickTimer.Stop();
            _suppressAction = true;
            if (Mouse.Captured == this) Mouse.Capture(null);
            ShowPanel();
            return;
        }
        _suppressAction = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
        Point p = e.GetPosition(this);
        Vector d = p - _downPoint;
        if (!_dragging && (Math.Abs(d.X) > SystemParameters.MinimumHorizontalDragDistance ||
                           Math.Abs(d.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _dragging = true;
        }
        if (_dragging)
        {
            Point screen = PointToScreen(p);
            Left += screen.X - _downScreen.X;
            Top += screen.Y - _downScreen.Y;
            _downScreen = screen;
            var work = SystemParameters.WorkArea;
            Left = Clamp(Left, work.Left, work.Right - Width);
            Top = Clamp(Top, work.Top, work.Bottom - Height);
        }
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        bool wasDown = _mouseDown;
        bool wasDrag = _dragging;
        _mouseDown = false;
        _dragging = false;
        if (Mouse.Captured == this) Mouse.Capture(null);
        if (!wasDown || wasDrag) return;
        if ((DateTime.UtcNow - _downTime).TotalMilliseconds > 650) return;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void OnClickTimerTick(object sender, EventArgs e)
    {
        _clickTimer.Stop();
        if (!_suppressAction)
        {
            TriggerRandomAction();
        }
        _suppressAction = false;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        ShowPanel();
    }

    private void TriggerRandomAction()
    {
        string action = Actions[_rng.Next(Actions.Length)];
        _model.TriggerAction(action, 2.6 + _rng.NextDouble() * 2.0);
    }

    private void ShowPanel()
    {
        if (_panel != null)
        {
            _panel.Close();
            _panel = null;
        }
        _panel = new PanelWindow(this);
        _panel.Owner = this;
        _panel.Show();
    }

    internal double CurrentSize
    {
        get { return Width; }
    }

    internal void ApplyScale(double sizePixels)
    {
        var work = SystemParameters.WorkArea;
        double max = Math.Max(110, work.Height / 2.0);
        double min = 96;
        double size = Math.Max(min, Math.Min(max, sizePixels));
        _settings.Scale = size / BaseSize;
        Width = size;
        Height = size;
        _model.GlobalScale.ScaleX = _settings.Scale;
        _model.GlobalScale.ScaleY = _settings.Scale;
        _model.GlobalScale.ScaleZ = _settings.Scale;
    }

    internal void SetTopmost(bool topmost)
    {
        Topmost = topmost;
        _settings.Topmost = topmost;
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.X = Left;
        _settings.Y = Top;
        _settings.Save();
        if (_panel != null)
        {
            _panel.Close();
            _panel = null;
        }
    }

    private static double Clamp(double v, double min, double max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}

public class PanelWindow : Window
{
    private readonly PetWindow _pet;
    private Slider _size;

    public PanelWindow(PetWindow pet)
    {
        _pet = pet;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(238, 252, 240, 217));
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 252, 240, 217)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 167, 92)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = BuildContent()
        };
        Content = border;

        Loaded += OnLoaded;
    }

    private UIElement BuildContent()
    {
        var panel = new StackPanel();

        var title = new TextBlock
        {
            Text = "\u5976\u86d9",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(91, 58, 33)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(title);

        var sizeLabel = new TextBlock
        {
            Text = "\u5927\u5c0f",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(91, 58, 33)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(sizeLabel);

        _size = new Slider
        {
            Minimum = 96,
            Maximum = Math.Max(110, SystemParameters.WorkArea.Height / 2.0),
            Value = Math.Max(96, _pet.CurrentSize),
            Width = 180,
            IsSnapToTickEnabled = false
        };
        _size.ValueChanged += OnSizeChanged;
        panel.Children.Add(_size);

        var sizeHint = new TextBlock
        {
            Text = "",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(143, 105, 64)),
            Margin = new Thickness(0, 2, 0, 12)
        };
        panel.Children.Add(sizeHint);
        _size.Tag = sizeHint;
        UpdateSizeHint();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var up = MakeButton("\u4e0a\u79fb", () => _pet.SetTopmost(true));
        var down = MakeButton("\u4e0b\u79fb", () => _pet.SetTopmost(false));
        buttons.Children.Add(up);
        buttons.Children.Add(down);
        panel.Children.Add(buttons);

        var close = MakeButton("\u5173\u95ed", Close);
        close.Margin = new Thickness(0, 10, 0, 0);
        close.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(close);

        var quit = MakeButton("\u9000\u51fa", delegate { Application.Current.Shutdown(); });
        quit.Margin = new Thickness(0, 5, 0, 0);
        quit.HorizontalAlignment = HorizontalAlignment.Center;
        quit.Background = new SolidColorBrush(Color.FromRgb(224, 147, 127));
        quit.BorderBrush = new SolidColorBrush(Color.FromRgb(184, 104, 86));
        panel.Children.Add(quit);
        return panel;
    }

    private Button MakeButton(string text, Action action)
    {
        var b = new Button
        {
            Content = text,
            MinWidth = 62,
            Height = 28,
            Margin = new Thickness(3, 0, 3, 0),
            Background = new SolidColorBrush(Color.FromRgb(226, 185, 122)),
            Foreground = new SolidColorBrush(Color.FromRgb(65, 42, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(187, 137, 72)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 12,
            Cursor = Cursors.Hand
        };
        b.Click += delegate { action(); };
        return b;
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _pet.ApplyScale(e.NewValue);
        UpdateSizeHint();
    }

    private void UpdateSizeHint()
    {
        if (_size != null && _size.Tag is TextBlock)
        {
            TextBlock hint = (TextBlock)_size.Tag;
            hint.Text = ((int)_size.Value).ToString(CultureInfo.InvariantCulture) + " px";
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        double left = _pet.Left + _pet.Width + 12;
        double top = _pet.Top;
        if (left + ActualWidth > work.Right) left = _pet.Left - ActualWidth - 12;
        if (top + ActualHeight > work.Bottom) top = work.Bottom - ActualHeight - 8;
        Left = Math.Max(work.Left + 4, left);
        Top = Math.Max(work.Top + 4, top);
    }
}
