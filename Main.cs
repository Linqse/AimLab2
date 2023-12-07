using System.Collections.Immutable;
using System.Numerics;
using EyeAuras.Graphics.ImageEffects;
using EyeAuras.OpenCVAuras.ML.Yolo;
using EyeAuras.Roxy.Shared;
using PoeShared.Native;
using EyeAuras.Graphics.Scaffolding;

namespace EyeAuras.Web.Repl.Component;

public partial class Main : WebUIComponent {
    private IInputSimulatorEx InputWindowsEx { get; init; }
    private IWindowHandleProvider WindowHandleProvider { get; init; }

    public Main([Dependency("WindowsInputSimulator")] IInputSimulatorEx inputWindowsEx,
        IWindowHandleProvider windowHandleProvider)
    {
        WindowHandleProvider = windowHandleProvider;
        InputWindowsEx = inputWindowsEx;
    }

    private IMLSearchTrigger ML => AuraTree.FindAuraByPath(@".\ML").Triggers.Items.OfType<IMLSearchTrigger>().First();

    private IHotkeyIsActiveTrigger Hotkey =>
        AuraTree.FindAuraByPath(@".\Key").Triggers.Items.OfType<IHotkeyIsActiveTrigger>().First();

    private Point center;
    private const int Full360 = 4800; // 4800 или 21428 - по идее pixel круг в 3D пространстве.
    private const double FOV = 52.0; // Значение FOV - область видимости на экране, меняется когда zoom weapon
    private const double sense = 1;
    private Rectangle window;
    
    protected override async Task HandleAfterFirstRender()
    {
        ML.ResultStream.Subscribe(x => Go(x.Predictions)).AddTo(Anchors);
        if (ML.ActiveWindow != null)
        {
            center = new Point(ML.ActiveWindow.DwmWindowBounds.Height/2, ML.ActiveWindow.DwmWindowBounds.Width / 2);
            window = ML.ActiveWindow.DwmWindowBounds;
        }
        
        
        
    }
    private bool isGGRunning = false;
    private async Task Go(ImmutableArray<YoloPrediction> predictions)
    {
        if (isGGRunning || ML.IsActive == false)
            return;

        isGGRunning = true;
        try
        {
            if (Hotkey.IsActive == true)
            {
                var sw = new Stopwatch();
                sw.Start();
                var result = FindClosestPrediction(predictions);
                Log.Info($"Find closet {sw.ElapsedMilliseconds}");
                var predict = CordsForMouseMove(result.X, result.Y, window.Width, window.Height);
                Log.Info($"CordsForMouseMove {sw.ElapsedMilliseconds}");
                if (predictions != null && predictions != default)
                {
                    Press(predict);
                    await Task.Delay(80);
                }

                Log.Info($"Finish {sw.ElapsedMilliseconds}");
                sw.Stop();

            }
        }
        catch
        {
            Log.Info("Sorry i have a BIG PROBLEM");
        }
        finally 
        {
            isGGRunning = false;
        }
    }
    private Point FindClosestPrediction(ImmutableArray<YoloPrediction> predictions)
    {
        return predictions.Select(ConvertToOriginalCoordinates)
            .OrderBy(p => DistanceSquared(center, p))
            .FirstOrDefault();
    }

    private Point ConvertToOriginalCoordinates(YoloPrediction prediction)
    {
        
        const int letterboxSize = 160;

        
        double scaleX = (double)window.Width / letterboxSize;
        double scaleY = (double)window.Height / letterboxSize;

        
        int originalX = (int)(prediction.Rectangle.X * scaleX);
        int originalY = (int)(prediction.Rectangle.Y * scaleY);
        int originalWidth = (int)(prediction.Rectangle.Width * scaleX);
        int originalHeight = (int)(prediction.Rectangle.Height * scaleY);

        
        int centerX = originalX + originalWidth / 2;
        int centerY = originalY + originalHeight / 2;

        return new Point(centerX, centerY);
    }
    
    
    private void Press(Point closestPredictionCenter)
    {
        using var controller = InputWindowsEx.Rent();
        
        controller.MoveMouseBy(ML.ActiveWindow, closestPredictionCenter.X, closestPredictionCenter.Y);
        controller.LeftButtonDown(ML.ActiveWindow, new Point(0,0));
        controller.LeftButtonUp(ML.ActiveWindow, new Point(0,0));
        //Thread.Sleep(80);
    }
    private static double DistanceSquared(Point p1, Point p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        return dx * dx + dy * dy;
    }
    
    public Point CordsForMouseMove(int targetX, int targetY, int screenWidth, int screenHeight)
    {
        
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;

        
        int deltaX = targetX - centerX;
        int deltaY = targetY - centerY;

        
        double angleX = CalculateAngle(deltaX, screenWidth);
        double angleY = CalculateAngle(deltaY, screenHeight);

        
        int moveX = (int)((Full360 * angleX) / 360);
        int moveY = (int)((Full360 * angleY) / 360);

        
        moveX = (int)(moveX * sense);
        moveY = (int)(moveY * sense);

        
        return new Point(moveX, moveY);
    }

    private double CalculateAngle(int delta, int total)
    {
        double lookAt = delta * 2.0 / total;
        return Math.Atan(lookAt * Math.Tan(FOV * Math.PI / 180)) * 180 / Math.PI;
    }
}