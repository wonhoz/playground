namespace OrbitCraft.Entities;

/// <summary>행성/항성 — 위치·질량·반지름·목표 여부.</summary>
public class Planet
{
    public double X, Y;
    public double Mass;
    public double Radius;
    public bool   IsTarget;
    public string Name = "";
}
