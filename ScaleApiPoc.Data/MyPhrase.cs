namespace ScaleApiPoc.Data;

public class MyPhrase
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public float value { get; set; }  // REAL in PostgreSQL
}
