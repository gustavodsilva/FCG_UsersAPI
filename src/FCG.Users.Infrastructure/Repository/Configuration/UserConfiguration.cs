using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FCG.Users.Domain.Entity;

namespace FCG.Users.Infrastructure.Repository.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnType("int").UseIdentityColumn();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(t => t.Email).IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
        builder.Property(t => t.Login).HasMaxLength(50).HasColumnType("varchar(50)");
        // O hash BCrypt tem 60 caracteres fixos. O HasMaxLength precisa acompanhar o
        // varchar(100) da coluna: se ficar menor que 60, o dia em que o HasColumnType
        // sair daqui o EF passa a truncar o hash na gravação — e o login de todos
        // quebra sem erro nenhum aparecer.
        builder.Property(t => t.Password).IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("Password");
        builder.Property(t => t.Perfil).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnType("DATETIME");
        builder.Property(t => t.UpdatedAt).HasColumnType("DATETIME");

        // Garantia no banco de que login e e-mail não se repetem (o service valida antes,
        // mas só o índice único protege contra duas requisições simultâneas).
        builder.HasIndex(t => t.Login).IsUnique();
        builder.HasIndex(t => t.Email).IsUnique();
    }
}