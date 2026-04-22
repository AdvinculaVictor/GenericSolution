namespace GenericSolution.Domain;

public class Cliente
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Email { get; set; }
    public string Domicilio { get; set; }
    public string CodigoPostal { get; set; }
    public string RFC { get; set; }
}
