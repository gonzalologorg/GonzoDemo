using UnityEngine;
using UnityEngine.UIElements;

public class RadialFill : VisualElement
{
	private float progress = 1f;

	public float Progress
	{
		get => progress;
		set
		{
			progress = Mathf.Clamp01(value);
			MarkDirtyRepaint();
		}
	}

	public RadialFill()
	{
		generateVisualContent += Draw;
	}

	private void Draw(MeshGenerationContext ctx)
	{
		if (progress <= 0f)
			return;

		var painter = ctx.painter2D;

		float radius = Mathf.Min(
			contentRect.width,
			contentRect.height
		) / 2f;

		Vector2 center = new Vector2(
			contentRect.width / 2f,
			contentRect.height / 2f
		);

		painter.fillColor = Color.white;
		painter.BeginPath();
		painter.MoveTo(center);

		painter.Arc(
			center,
			radius,
			-90f,
			-90f + 360f * progress
		);

		painter.ClosePath();
		painter.Fill();
	}
}