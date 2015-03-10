// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.InMemory
{
    public class InMemoryDatabaseFactory : IInMemoryDatabaseFactory
    {
        private readonly DbContext _context;
        private readonly IInMemoryDataStoreCreator _dataStoreCreator;
        private readonly ILoggerFactory _loggerFactory;

        public InMemoryDatabaseFactory(
            [NotNull] DbContext context,
            [NotNull] IInMemoryDataStoreCreator dataStoreCreator,
            [NotNull] ILoggerFactory loggerFactory)
        {
            Check.NotNull(context, nameof(context));
            Check.NotNull(dataStoreCreator, nameof(dataStoreCreator));
            Check.NotNull(loggerFactory, nameof(loggerFactory));

            _context = context;
            _dataStoreCreator = dataStoreCreator;
            _loggerFactory = loggerFactory;
        }

        public virtual Database CreateDatabase() => new InMemoryDatabaseFacade(_context, _dataStoreCreator, _loggerFactory);
    }
}
