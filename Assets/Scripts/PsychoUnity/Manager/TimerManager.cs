using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;
using ThreadPriority = System.Threading.ThreadPriority;

namespace PsychoUnity.Manager
{
    public class TimerManager : Singleton<TimerManager>
    {
        private readonly Dictionary<string, TimerBase> _timersDic = new Dictionary<string, TimerBase>();

        /// <summary>
        /// Retrieves the frequency of the performance counter, return the current performance-counter frequency,
        /// in counts per second.
        /// </summary>
        public static long Frequency
        {
            get
            {
                TimerHighResolution.QueryPerformanceFrequency(out var value);
                return value;
            }
        }

        /// <summary>
        /// Calling Win32API. Retrieves the current value of the performance counter, which is a high resolution
        /// (less than 1us) time stamp that can be used for time-interval measurements.
        /// </summary>
        /// <returns> the current performance-counter value, in counts </returns>
        public static long GetTimestamp()
        {
            long timestamp = 0;
            TimerHighResolution.QueryPerformanceCounter(ref timestamp);

            return timestamp;
        }

        public void Create(string name, TimerType type, int core = 4)
        {
            if (!_timersDic.ContainsKey(name))
            {
                _timersDic[name] = type is TimerType.Normal ? new TimerNormal() : new TimerHighResolution(core);
            }
            else
            {
                Debug.Log($"Timer {name} already exist");
            }
        }

        public void SetSchedule(string name, int duration, int delay = 0, int times = 1, UnityAction action = null)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].SetTimer(duration, delay, times, action);
            }
        }

        public void AddTask(string name, UnityAction action)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].AddTask(action);
            }
        }

        public void Start(string name)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].Start();
            }
        }

        public void Stop(string name)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].Stop();
            }
        }

        public void Pause(string name)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].Pause();
            }
        }

        public void ReStart(string name)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].Restart();
            }
        }

        public void Destroy(string name)
        {
            if (VerifyTimer(name))
            {
                _timersDic[name].Destroy();
            }
        }

        private bool VerifyTimer(string name)
        {
            if (_timersDic.ContainsKey(name)) return true;
            Debug.Log($"Timer {name} does not exist");
            return false;

        }

        public enum TimerType
        {
            Normal,
            HighResolution
        }
    }

    public abstract class TimerBase
    {
        public abstract void SetTimer(int duration, int delay, int times, UnityAction action);

        public abstract void Start();

        public abstract void AddTask(UnityAction action);

        public abstract void Pause();

        public abstract void Restart();

        public abstract void Stop();

        public abstract void Destroy();

    }

    public class TimerNormal : TimerBase
    {
        private int _duration;
        private int _delay;
        private int _times;
        private UnityAction _action;

        private Task _taskHandler;

        public override void SetTimer(int duration, int delay, int times, UnityAction action)
        {
            _duration = duration;
            _delay = delay;
            _times = times;
            _action = action;
        }

        public override void Start()
        {
            _taskHandler = Timing();
        }

        public override void AddTask(UnityAction action)
        {
            _action += action;
        }

        public override void Stop()
        {
            _taskHandler.Dispose();
        }

        public override void Pause()
        {
            _taskHandler.Wait();
        }

        public override void Restart()
        {
            _taskHandler.Start();
        }

        public override void Destroy(){
            _taskHandler.Dispose();
        }

        private async Task Timing()
        {
            if (_delay > 0)
            {
                await Task.Delay(_delay);
            }

            var count = 0;
            while (count < _times)
            {
                await Task.Delay(_duration);
                _action.Invoke();

                count++;
            }
        }
    }
    public class TimerHighResolution : TimerBase
    {
        private long _duration; // 
        private int _delay;
        private int _times; // Times
        private UnityAction _action;
        
        private readonly Thread _timingThread;
        private bool _keepThread;
        private bool _startTiming;
        private bool _pauseTiming;

        private long _startPoint;
        private long _endPoint;

        private readonly IntPtr _core;
        
        public TimerHighResolution(int core)
        {
            _timingThread = new Thread(Timing)
            {
                Priority = ThreadPriority.Highest
            };
            _keepThread = true;
            _core = (IntPtr)core;
            _timingThread.Start();
        }

        public override void SetTimer(int duration, int delay, int times, UnityAction action)
        {
            QueryPerformanceFrequency(out var frequency);
            _duration = frequency / 1000 * duration;
            _delay = delay;
            _times = times;
            _action = action;
        }
        
        public override void AddTask(UnityAction action)
        {
            _action += action;
        }

        public override void Start()
        {
            _startTiming = true;
        }

        //TODO
        public override void Pause()
        {
            _pauseTiming = true;
        }

        //TODO
        public override void Restart()
        {
            _pauseTiming = false;
        }

        public override void Stop()
        {
            _startTiming = false;
        }

        public override void Destroy()
        {
            _startTiming = false;
            _keepThread = false;
            _timingThread.Join();
        }

        private void Timing()
        {
            SetCPUCore();
            
            var count = 0;
            
            while (_keepThread)
            {
                WaitForTiming();
                
                Delay();
                
                MainTiming(ref count);
            }
        }

        private void SetCPUCore()
        {
            var threadHandle = GetCurrentThread();
            SetThreadAffinityMask(threadHandle, _core);
        }

        private void WaitForTiming()
        {
            while (!_startTiming)
            {
                if (_keepThread == false)
                {
                    break;
                }
            }
        }

        private void Delay()
        {
            if (_delay <= 0) return;
            QueryPerformanceCounter(ref _startPoint);
            while (_startTiming)
            {
                QueryPerformanceCounter(ref _endPoint);
                if ((_endPoint - _startPoint) > _delay)
                {
                    break;
                }
            }
        }
        private void MainTiming(ref int count)
        {
            var duration = _duration;
            var remainTime = _duration;
            QueryPerformanceCounter(ref _startPoint);
            while (count < _times && _startTiming)
            {
                while (_pauseTiming)
                {
                    duration = remainTime;
                    QueryPerformanceCounter(ref _startPoint);
                }
                remainTime = duration - (_endPoint - _startPoint);
                QueryPerformanceCounter(ref _endPoint);
                if (remainTime > 0) continue;
                _action.Invoke();
                count++;
                QueryPerformanceCounter(ref _startPoint);
            }
        }
        
        [DllImport("kernel32.dll")]
        public static extern bool QueryPerformanceCounter(ref long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        public static extern bool QueryPerformanceFrequency(out long lpFrequency);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();
    }
}
