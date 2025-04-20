using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.Tweens
{
    public static unsafe class TweenUtilities
    {
        public static void PlaySequence(bool reset, ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2)
        {
            int timersCount = 2;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;

            TweenUtilities.PlaySequence(reset, ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
        }

        public static void PlaySequence(bool reset, ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2, ref TweenTimer timer3)
        {
            int timersCount = 3;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;
            timers[2] = timer3;

            TweenUtilities.PlaySequence(reset, ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
            timer3 = timers[2];
        }

        public static void PlaySequence(bool reset, ref sbyte state, TweenTimer* timers, int timersCount)
        {
            if(timersCount <= 0)
                return;

            RefreshSequenceState(ref state, out int absoluteState, out int currentTimerIndex);

            if (reset)
            {
                state = 1;
                RefreshSequenceState(ref state, out absoluteState, out currentTimerIndex);

                for (int i = 1; i < timersCount; i++)
                {
                    TweenTimer otherTimer = timers[i];
                    otherTimer.Stop();
                    timers[i] = otherTimer;
                }
                TweenTimer timer = timers[currentTimerIndex];
                timer.Play(true);
                timers[currentTimerIndex] = timer;
            }
            else
            {
                TweenTimer timer = timers[currentTimerIndex];
                timer.Play(false);
                timers[currentTimerIndex] = timer;
            }
        }

        public static void SetSequenceCourse(bool forward, ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2)
        {
            int timersCount = 2;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;

            TweenUtilities.SetSequenceCourse(forward, ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
        }

        public static void SetSequenceCourse(bool forward, ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2, ref TweenTimer timer3)
        {
            int timersCount = 3;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;
            timers[2] = timer3;

            TweenUtilities.SetSequenceCourse(forward, ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
            timer3 = timers[2];
        }

        public static void SetSequenceCourse(bool forward, ref sbyte state, TweenTimer* timers, int timersCount)
        {
            if (timersCount <= 0)
                return;

            RefreshSequenceState(ref state, out int absoluteState, out int currentTimerIndex);

            if (forward)
            {
                // Invert state if we were in reverse
                if(state < 0)
                {
                    state = (sbyte)(-state);
                    RefreshSequenceState(ref state, out absoluteState, out currentTimerIndex);
                }
                TweenTimer timer = timers[currentTimerIndex];
                timer.SetCourse(true);
                timers[currentTimerIndex] = timer;
            }
            else
            {
                // Invert state if we were in forward
                if (state > 0)
                {
                    state = (sbyte)(-state);
                    RefreshSequenceState(ref state, out absoluteState, out currentTimerIndex);
                }
                TweenTimer timer = timers[currentTimerIndex];
                timer.SetCourse(false);
                timers[currentTimerIndex] = timer;
            }
        }

        public static void UpdateSequence(ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2)
        {
            int timersCount = 2;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;

            TweenUtilities.UpdateSequence(ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
        }

        public static void UpdateSequence(ref sbyte state, ref TweenTimer timer1, ref TweenTimer timer2, ref TweenTimer timer3)
        {
            int timersCount = 3;

            TweenTimer* timers = stackalloc TweenTimer[timersCount];
            timers[0] = timer1;
            timers[1] = timer2;
            timers[2] = timer3;

            TweenUtilities.UpdateSequence(ref state, timers, timersCount);

            timer1 = timers[0];
            timer2 = timers[1];
            timer3 = timers[2];
        }

        public static void UpdateSequence(ref sbyte state, TweenTimer* timers, int timersCount)
        {
            if (timersCount <= 0)
                return;

            RefreshSequenceState(ref state, out int absoluteState, out int currentTimerIndex);

            if (timers[currentTimerIndex].HasCompleted())
            {
                TweenTimer prevTimer = timers[currentTimerIndex];

                // Detect starting next timer in forward sequence
                if (state > 0 && state < timersCount)
                {
                    state++;
                    RefreshSequenceState(ref state, out absoluteState, out currentTimerIndex);
                    TweenTimer newTimer = timers[currentTimerIndex];
                    newTimer.SetCourse(true);
                    newTimer.SetTime(prevTimer.GetExcessTime());
                    newTimer.Play(false);
                    timers[currentTimerIndex] = newTimer;
                }
                // Detect starting next timer in reverse sequence
                else if (state < 0 && state < -1)
                {
                    state++;
                    RefreshSequenceState(ref state, out absoluteState, out currentTimerIndex);
                    TweenTimer newTimer = timers[currentTimerIndex];
                    newTimer.SetCourse(false);
                    newTimer.SetTime(newTimer.GetDuration() - prevTimer.GetExcessTime());
                    newTimer.Play(false);
                    timers[currentTimerIndex] = newTimer;
                }
            }
        }

        private static void RefreshSequenceState(ref sbyte state, out int absoluteState, out int currentTimerIndex)
        {
            if (state == 0)
            {
                state = 1;
            }

            absoluteState = math.abs(state);
            currentTimerIndex = (sbyte)(absoluteState - 1);
        }
    }
}
