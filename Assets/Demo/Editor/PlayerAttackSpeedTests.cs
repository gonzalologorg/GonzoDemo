using GonzoDemo;
using NUnit.Framework;

public class PlayerAttackSpeedTests
{
    [TestCase(1f, 2f, 1f, 0.5f)]
    [TestCase(1f, 0.5f, 1f, 2f)]
    [TestCase(0.7f, 2f, 1f, 0.5f)]
    [TestCase(1.2f, 2.5f, 1.25f, 0.5f)]
    public void WholeAnimationFitsAttackInterval(float clipLength, float speed, float cooldown, float duration)
    {
        float interval = PlayerCombat.CalculateAttackInterval(speed, cooldown);
        float playbackSpeed = PlayerCombat.CalculateAttackPlaybackSpeed(clipLength, interval);
        Assert.That(clipLength / playbackSpeed, Is.EqualTo(duration).Within(0.00001f));
    }

    [TestCase(2.5f, 1f, 0.4f)]
    [TestCase(2.5f, 1.25f, 0.5f)]
    [TestCase(1f, 1f, 1f)]
    [TestCase(50f, 1f, 0.02f)]
    public void CooldownMultipliesReciprocalAttackSpeed(float speed, float multiplier, float expected)
    {
        Assert.That(PlayerCombat.CalculateAttackInterval(speed, multiplier), Is.EqualTo(expected).Within(0.00001f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidSpeedDisablesAttacks(float speed)
    {
        Assert.That(PlayerCombat.CalculateAttackInterval(speed, 1f), Is.EqualTo(float.PositiveInfinity));
    }
}
