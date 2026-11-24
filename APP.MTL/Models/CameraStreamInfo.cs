namespace MTL.APP.Models;

public class CameraStreamInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RtspUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
