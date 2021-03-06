﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Entity.FunctionalTests;
using Microsoft.Data.Entity.InMemory;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Framework.DependencyInjection;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class NavigationTest
    {
        [Fact]
        public void Duplicate_entries_are_not_created_for_navigations_to_principal()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryStore()
                .AddDbContext<GoTContext>()
                .ServiceCollection()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<GoTContext>())
            {
                context.ConfigAction = (modelBuilder) =>
                    {

                        modelBuilder.Entity<Person>().HasMany(p => p.Siblings).WithOne(p => p.SiblingReverse).Required(false);
                        modelBuilder.Entity<Person>().HasOne(p => p.Lover).WithOne(p => p.LoverReverse).Required(false);
                        return 0;
                    };

                var model = context.Model;
                var entityType = model.EntityTypes.First();

                Assert.Equal("'Person' {'LoverId'} -> 'Person' {'Id'}", entityType.ForeignKeys.First().ToString());
                Assert.Equal("'Person' {'SiblingReverseId'} -> 'Person' {'Id'}", entityType.ForeignKeys.Skip(1).First().ToString());
            }
        }

        [Fact]
        public void Duplicate_entries_are_not_created_for_navigations_to_dependant()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryStore()
                .AddDbContext<GoTContext>()
                .ServiceCollection()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<GoTContext>())
            {
                context.ConfigAction = (modelBuilder) =>
                    {

                        modelBuilder.Entity<Person>().HasOne(p => p.SiblingReverse).WithMany(p => p.Siblings).Required(false);
                        modelBuilder.Entity<Person>().HasOne(p => p.Lover).WithOne(p => p.LoverReverse).Required(false);
                        return 0;
                    };

                var model = context.Model;
                var entityType = model.EntityTypes.First();

                Assert.Equal("'Person' {'LoverId'} -> 'Person' {'Id'}", entityType.ForeignKeys.First().ToString());
                Assert.Equal("'Person' {'SiblingReverseId'} -> 'Person' {'Id'}", entityType.ForeignKeys.Skip(1).First().ToString());
            }
        }
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public List<Person> Siblings { get; set; }
        public Person Lover { get; set; }
        public Person LoverReverse { get; set; }
        public Person SiblingReverse { get; set; }
    }

    public class GoTContext : DbContext
    {
        public DbSet<Person> People { get; set; }
        public Func<ModelBuilder, int> ConfigAction { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigAction.Invoke(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptions options)
        {
            options.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=StateManagerBug;Trusted_Connection=True;MultipleActiveResultSets=true");
        }
    }
}

