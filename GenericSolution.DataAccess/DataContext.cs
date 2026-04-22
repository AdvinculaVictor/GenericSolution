using GenericSolution.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenericSolution.DataAccess;

public class DataContext: DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    // Represents the 'Clientes' table
    public DbSet<Cliente> Clientes { get; set; }

    // Represents the 'Categorias' table
    public DbSet<Categoria> Categorias { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
