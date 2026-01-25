using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntIndex.Interfaces;
using AntIndex.Models.Index;
using AntIndex.Services.Extensions;
using AntIndex.Services.Normalizing;
using AntIndex.Services.Splitting;

namespace AntIndex.UnitTests;

[TestFixture]
public class IndexBuildingTests
{
    record IndexingEntity(Key key, string name) : IIndexedEntity
    {
        public Key GetKey()
            => key;

        public IEnumerable<Phrase> GetNames()
            => [Ant.Phrase(name)];

        public IEnumerable<Key> GetLinks()
            => [];

        public Key? GetContainer()
            => null;
    }
}
