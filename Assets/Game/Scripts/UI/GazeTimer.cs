﻿using UnityEngine;

public class GazeTimer
{
    public delegate void PointerClickDelegate();

    public delegate float GetGazeTimeDelegate();

    public bool Clicked;
    public bool Gazing;

    private readonly PointerClickDelegate _pointerClick;
    private readonly GetGazeTimeDelegate _gazeTime;

    private float _gazeStartTime;

    public GazeTimer(PointerClickDelegate pointerClick, GetGazeTimeDelegate gazeTime)
    {
        _pointerClick = pointerClick;
        _gazeTime = gazeTime;
    }

    public void PointerEnter()
    {
        _gazeStartTime = Time.time;
        Gazing = true;
    }

    public void PointerExit()
    {
        _gazeStartTime = 0;
        Gazing = false;
        Clicked = false;
    }

    public void PointerClick()
    {
        // The click should not happen right after the player looks away, but he can click manually multiple times
        if (!Gazing && !Clicked)
        {
            return;
        }
        _pointerClick();
        Clicked = true;
        Gazing = false;
    }

    public void Update()
    {
        // Only the first click during a single gaze counts
        if (Gazing && !Clicked && Time.time > _gazeStartTime + _gazeTime())
        {
            PointerClick();
        }
    }
}
