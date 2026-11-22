using System;
using UnityEngine;

public class DoorOpenOnHover : MonoBehaviour
{
	[SerializeField] private float startAngle;
	[SerializeField] private float endAngle;
	[SerializeField] private float openTime;
	[SerializeField] private Transform door;
	//[SerializeField] private RectTransform textPivot;
	//[SerializeField] private Vector3 textEndPoint;
	[SerializeField] private AnimationCurve curve;
	[SerializeField] private Vector3 angleAxis = Vector3.up;

	private Vector3 textStartPoint;
	private bool hovering = false;
	private float progress = 0;
	private MenuHover menuHover;

	private void Start()
	{
		menuHover = GetComponent<MenuHover>();
		//textStartPoint = textPivot.position;
	}

	private void Update()
	{
		hovering = menuHover.isHovering;
		
		if (hovering && progress < 1)
		{
			progress += Time.deltaTime / openTime;
			progress = Mathf.Clamp01(progress);
			RotateDoor();
		}
		
		if (!hovering && progress > 0)
		{
			progress -= Time.deltaTime / openTime;
			progress = Mathf.Clamp01(progress);
			RotateDoor();
		}
	}

	private void RotateDoor()
	{
		float curveY = curve.Evaluate(progress);
		float newAngle = Mathf.Lerp(startAngle, endAngle, curveY);

		door.eulerAngles = newAngle * angleAxis;
		//textPivot.rotation = Quaternion.Euler(newAngle * angleAxis);
		//textPivot.position = Vector3.Lerp(textStartPoint, textEndPoint, curveY);
	}
}
