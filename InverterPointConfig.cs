namespace InverterHistorian;

public enum InverterPointType
{
    Digital,
    Analog,
    String,
}
public class InverterPointConfig
{
    public InverterPointType Type { get; set; }
    public double? Deadband { get; set; }
}