using BookCatAPI.Models;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace BookCatAPI.Data
{
    public class BookCatDbContext : DbContext
    {
        public BookCatDbContext(DbContextOptions<BookCatDbContext> options)
        : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<Library> Libraries { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AdminData> AdminData { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasMany(u => u.Libraries)
                .WithOne(l => l.User)
                .HasForeignKey(l => l.UserId);

            modelBuilder.Entity<Plan>()
                .HasMany(p => p.Libraries)
                .WithOne(l => l.Plan)
                .HasForeignKey(l => l.PlanId);

            modelBuilder.Entity<Library>()
                .HasMany(l => l.Books)
                .WithOne(b => b.Library)
                .HasForeignKey(b => b.LibraryId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Library>()
                .HasMany(l => l.Documents)
                .WithOne(d => d.Library)
                .HasForeignKey(d => d.LibraryId)
                .OnDelete(DeleteBehavior.Restrict);

        }

    }


}
