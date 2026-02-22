using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DashCity.Engine;

/// <summary>
/// 3D 메시 생성 유틸리티.
/// </summary>
public static class MeshHelper
{
    public static GeometryModel3D CreateBox(double w, double h, double d, Color color, double emissive = 0)
    {
        double hw = w / 2, hh = h / 2, hd = d / 2;
        var mesh = new MeshGeometry3D();

        Point3D[] c =
        [
            new(-hw, -hh, -hd), new(hw, -hh, -hd), new(hw, hh, -hd), new(-hw, hh, -hd),
            new(-hw, -hh, hd), new(hw, -hh, hd), new(hw, hh, hd), new(-hw, hh, hd)
        ];

        int[][] faces = [[0,1,2,3],[5,4,7,6],[4,0,3,7],[1,5,6,2],[3,2,6,7],[4,5,1,0]];
        Vector3D[] normals = [new(0,0,-1),new(0,0,1),new(-1,0,0),new(1,0,0),new(0,1,0),new(0,-1,0)];

        for (int f = 0; f < 6; f++)
        {
            int b = mesh.Positions.Count;
            for (int v = 0; v < 4; v++)
            {
                mesh.Positions.Add(c[faces[f][v]]);
                mesh.Normals.Add(normals[f]);
            }
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1); mesh.TriangleIndices.Add(b + 2);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 2); mesh.TriangleIndices.Add(b + 3);
        }

        return new GeometryModel3D(mesh, BuildMaterial(color, emissive));
    }

    public static GeometryModel3D CreatePlane(double w, double d, Color color, double emissive = 0.1)
    {
        double hw = w / 2, hd = d / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = { new(-hw, 0, -hd), new(hw, 0, -hd), new(hw, 0, hd), new(-hw, 0, hd) },
            Normals = { new(0, 1, 0), new(0, 1, 0), new(0, 1, 0), new(0, 1, 0) },
            TriangleIndices = { 0, 1, 2, 0, 2, 3 }
        };
        var mat = BuildMaterial(color, emissive);
        return new GeometryModel3D(mesh, mat) { BackMaterial = mat };
    }

    public static GeometryModel3D CreateCylinder(double radius, double height, int segments, Color color, double emissive = 0)
    {
        var mesh = new MeshGeometry3D();
        double hh = height / 2;

        for (int i = 0; i < segments; i++)
        {
            double a1 = 2 * Math.PI * i / segments;
            double a2 = 2 * Math.PI * (i + 1) / segments;
            double x1 = Math.Cos(a1) * radius, z1 = Math.Sin(a1) * radius;
            double x2 = Math.Cos(a2) * radius, z2 = Math.Sin(a2) * radius;

            int b = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(x1, -hh, z1));
            mesh.Positions.Add(new Point3D(x2, -hh, z2));
            mesh.Positions.Add(new Point3D(x2, hh, z2));
            mesh.Positions.Add(new Point3D(x1, hh, z1));

            var n = new Vector3D((x1 + x2) / 2, 0, (z1 + z2) / 2);
            n.Normalize();
            for (int j = 0; j < 4; j++) mesh.Normals.Add(n);

            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1); mesh.TriangleIndices.Add(b + 2);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 2); mesh.TriangleIndices.Add(b + 3);
        }

        return new GeometryModel3D(mesh, BuildMaterial(color, emissive));
    }

    private static MaterialGroup BuildMaterial(Color color, double emissive)
    {
        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        if (emissive > 0)
        {
            byte er = (byte)Math.Min(255, color.R * emissive);
            byte eg = (byte)Math.Min(255, color.G * emissive);
            byte eb = (byte)Math.Min(255, color.B * emissive);
            mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(er, eg, eb))));
        }
        return mat;
    }

    public static void SetPosition(GeometryModel3D model, double x, double y, double z)
    {
        model.Transform = new TranslateTransform3D(x, y, z);
    }

    public static void SetPositionAndRotation(GeometryModel3D model, double x, double y, double z, double angleDeg)
    {
        var group = new Transform3DGroup();
        group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), angleDeg)));
        group.Children.Add(new TranslateTransform3D(x, y, z));
        model.Transform = group;
    }
}
