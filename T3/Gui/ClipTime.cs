﻿using ImGuiNET;
using ManagedBass;
using T3.Core.Operator;

namespace T3.Gui
{
    public class ClipTime
    {
        public virtual double Time { get; set; }
        public double TimeRangeStart { get; set; } = 0;
        public double TimeRangeEnd { get; set; } = 8;
        public double Bpm { get; set; } = 95.08f;
        public virtual double PlaybackSpeed { get; set; } = 0;
        public bool IsLooping = false;
        public TimeModes TimeMode { get; set; } = TimeModes.Bars;

        public int Bar => (int)(Time * Bpm / 60.0 / 4.0) + 1;
        public int Beat => (int)(Time * Bpm / 60.0) % 4 + 1;
        public int Tick => (int)(Time * Bpm / 60.0 * 4) % 4 + 1;

        public void Update()
        {
            UpdateTime();
            if (IsLooping && Time > TimeRangeEnd)
            {
                Time = Time - TimeRangeEnd > 1.0 // Jump to start if too far out of time region
                           ? TimeRangeStart
                           : Time - (TimeRangeEnd - TimeRangeStart);
            }
            EvaluationContext.GlobalTime = Time;
        }

        public enum TimeModes
        {
            Secs,
            Bars,
            F30,
            F60,
        }

        protected virtual void UpdateTime()
        {
            Time += ImGui.GetIO().DeltaTime * PlaybackSpeed;
        }
    }
    
    public class StreamClipTime : ClipTime
    {
        private readonly int _soundStreamHandle;
        
        public StreamClipTime(string filename)
        {
            Bass.Init();
            _soundStreamHandle = Bass.CreateStream(filename);
        }

        public override double Time
        {
            get => GetCurrentStreamTime();
            set
            {
                long soundStreamPos = Bass.ChannelSeconds2Bytes(_soundStreamHandle, value);
                Bass.ChannelSetPosition(_soundStreamHandle, soundStreamPos);
            }
        }

        private double _playbackSpeed; 
        public override double PlaybackSpeed
        {
            get
            {
                var playbackState = Bass.ChannelIsActive(_soundStreamHandle);
                return playbackState == PlaybackState.Playing ? 1.0 : 0.0;
            }

            set
            {
                _playbackSpeed = value;
                if (value == 0.0)
                {
                    Bass.ChannelStop(_soundStreamHandle);
                }
                else if (value < 0.0)
                {
                    Bass.ChannelStop(_soundStreamHandle);
                }
                else
                {
                    var playbackState = Bass.ChannelIsActive(_soundStreamHandle);
                    if (playbackState != PlaybackState.Playing)
                    {
                        Bass.ChannelPlay(_soundStreamHandle);
                    }
                }
            }
        }
        
        protected override void UpdateTime()
        {
            if (_playbackSpeed < 0.0)
            {
                // bass can't play backwards, so do it manually
                Time += ImGui.GetIO().DeltaTime * _playbackSpeed;
            }
        }

        private double GetCurrentStreamTime()
        {
            long soundStreamPos = Bass.ChannelGetPosition(_soundStreamHandle);
            return Bass.ChannelBytes2Seconds(_soundStreamHandle, soundStreamPos);
        }
    }
}
