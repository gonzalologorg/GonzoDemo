namespace GonzoDemo.Combat
{
    /// <summary>Receives hit events from weapons, projectiles, or other gameplay systems.</summary>
    public interface IHittable
    {
        void OnHit(HitInfo info);
    }
}
