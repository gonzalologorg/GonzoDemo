using System.Collections;
using UnityEngine;
namespace GonzoDemo
{
    /// <summary>Server-owned state shared with a behaviour for the lifetime of a held cast.</summary>
    public sealed class JutsuHoldSession
    {
        public float MaxDuration { get; }
        public float Power { get; private set; }
        public bool IsComplete { get; private set; }
        public bool WasReleased { get; private set; }

        private readonly double startTime;

        internal JutsuHoldSession(float maxDuration, double startTime)
        {
            MaxDuration = Mathf.Max(0.01f, maxDuration);
            this.startTime = startTime;
        }

        internal void Complete(double serverTime, bool wasReleased)
        {
            if (IsComplete)
                return;

            Power = Mathf.Clamp01((float)((serverTime - startTime) / MaxDuration));
            WasReleased = wasReleased;
            IsComplete = true;
        }
    }

    public class JutsuBehaviour : ScriptableObject
    {
        /// <summary>Returns zero for a tap cast; positive durations opt into the reusable Hold flow.</summary>
        public virtual float GetHoldDuration(JutsuContext ctx) => 0f;

        public virtual bool PreCast(JutsuContext ctx)
        {
            return true;
        }

        public virtual IEnumerator OnServerCast(JutsuContext ctx)
        {
            yield break;
        }

        public virtual IEnumerator OnServerHoldCast(JutsuContext ctx, JutsuHoldSession session)
        {
            yield break;
        }

        public virtual void OnServerHoldComplete(JutsuContext ctx, float power, bool wasReleased) { }

        public virtual void OnClientCast(JutsuContext ctx) { }
        public virtual void OnSelect(JutsuContext ctx) { }
        public virtual void OnUnequip(JutsuContext ctx) { }
        public virtual void OnEquip(JutsuContext ctx) { }
        public virtual void OnRender(JutsuContext ctx) { }
        public virtual void DrawHUD(JutsuContext ctx) { }
    }
}
