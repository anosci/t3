﻿using System.Diagnostics;
using SharpDX;
using T3.Core.Operator.Interfaces;
using Vector3 = SharpDX.Vector3;

namespace T3.Core.Operator
{
    public class EvaluationContext
    {
        public EvaluationContext()
        {
            Reset();
        }

        public void Reset()
        {
            TimeInBars = GlobalTimeInBars;
        }

        public void SetViewFromCamera(ICamera camera)
        {
            var fov = MathUtil.DegreesToRadians(45);
            float aspectRatio = (float)RequestedResolution.Width / RequestedResolution.Height;
            CameraToClipSpace = Matrix.PerspectiveFovRH(fov, aspectRatio, 0.01f, 1000);

            Vector3 eye = new Vector3(camera.CameraPosition.X, camera.CameraPosition.Y, camera.CameraPosition.Z);
            Vector3 target = new Vector3(camera.CameraTarget.X, camera.CameraTarget.Y, camera.CameraTarget.Z);
            Vector3 up = Vector3.Up;
            WorldToCamera = Matrix.LookAtRH(eye, target, up);

            ObjectToWorld = Matrix.Identity;
        }

        private static ICamera _defaultCamera = new ViewCamera();

        private static readonly Stopwatch _runTimeWatch = Stopwatch.StartNew();
        public static double RunTimeInSecs => _runTimeWatch.ElapsedMilliseconds / 1000.0;
        public static double GlobalTimeInBars { get; set; }
        
        /// <summary>
        /// The primary time used for user interactions and keyframe manipulation.
        /// This is where there time marker in the timeline is displayed. 
        /// </summary>
        public double TimeInBars { get; set; }
        
                
        /// <summary>
        /// If "keep running" option is enabled, this time is still running even if (audio) playback has been stopped.
        /// This is used by most procedural time related operators (like pulsate).  
        /// </summary>
        public static double GlobalTimeInSecs { get; set; }
        
        public static double BeatTime { get; set; }
        public static double  LastFrameDuration { get; set; }
        
        public Size2 RequestedResolution { get; set; }

        public Matrix CameraToClipSpace { get; set; } = Matrix.Identity;
        public Matrix WorldToCamera { get; set; } = Matrix.Identity;
        public Matrix ObjectToWorld { get; set; } = Matrix.Identity;
        public GizmoVisibility ShowGizmos { get; set; }

        public enum GizmoVisibility
        {
            Inherit = -1,
            Off = 0,
            On = 1,
        }
    }
}