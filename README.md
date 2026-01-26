# AntIndex

Linked data text search engine.


## Features

- Fast text search using 2-gramm words index
- Designed to search for entities of different types (up to 250 types)
- Support for data hierarchy and relatedness
- Flexible sorting settings
- MessagePack binary serialization enabled index file saving utilities


## Documentation

### Normalizing and splitting
The library provides built-in tools for normalizing and splitting a name into words for searching. Use this static objects.

AntIndex.Services.Normalizing.DefaultNormalizer.Instance
AntIndex.Services.Splitting.DefaultPhraseSplitter.Instance

Also, you can redefine normalization and phrases splitting on words for use in your cases. Implement: IPhraseSplitter and INormalizer

### Defining entities of search
- Your search entity must implement the interface **IIndexedEntity**
    - GetKey() - Defines an entity keys
        - Use Ant.Key() for create key, _entityType must be greater than 0_
    - GetNames() - Define the phrases by which the search will be carried out
        - Use the _Ant.Phrase()_ method and its overloads to specify names to search for and to define the name type for flexible sorting and scoring.
        - If you are using the extended phrase configuration Ant.Phrase(string phrase, byte phraseType), use phraseType > 0
    - GetLinks() - Identify the keys that are associated with your entity (when creating an index, the entity will be a child of each element in the Links list)
    - GetContainer() - Determine the parent if the entity cannot be found outside the given parent's hierarchy

### Build index

**Building**

- Use Ant.Build() to build Anthill index isntance, passing the normalizer and splitter instances to the method, as well as an enumeration entities for searching
- Also, you can get an instance of the builder using Ant.GetBuilder(INormalizer normalizer, IPhraseSplitter phraseSplitter)
    - Call builder _AddEntity_ method to add entity (multithreading is not working)
    - Call _Build_ to get Anthill instance

**Save**

- Use Ant.WriteIndex(AntHill index, string filePath) for saving index file
- Use Ant.ReadIndex(string filePath) for read index file

### Search

**Create SearchContext** 

Implement abstract SearchContextBase
- Implement SearchContextBase.Request property (array of requests to search) to configure your search request. The query options are presented below (use in the same order for proper operation)
    - Search - search current type entities
    - SearchBy - search current type entities in parents hierarchy (parents must be found in the Search block above)
    - Select - performs forced addition of entities of the target type based on the passed ids
    - AppendChilds - use to force adding entities by parent
- Override GetLinkedEntityMatchMiltipler(byte entityType, byte linkedType) for flexible scoring mathes of linked entities
- Override GetPhraseTypeMultipler(byte phraseType) for flexible scoring by phrase types
- Override OnLinkedEntityMatched(Key entityKey, Key linkedKey) to add individual sorting rules if linked entity is match
- Override OnEntityProcessed(EntityMatchesBundle entityMatchesBundle) to add individual sorting rules
- Override ResultVisionFilter(byte type, IEnumerable<EntityMatchesBundle> result) to filter the results that will be output as a result

**Search**

- Use AntHill instance method Search passing the search context
- Or use AntHill instance method SearchTypes passing the search context and list of target entities with their count


## Optimizations

- If your entity cannot be found if the hierarchy parent is not found, be sure to set the IIndexedEntity.Parent field to improve performance.
- If you using overrides OnLinkedEntityMatched or OnEntityProcessed use a static AdditionalRule intances for smaller memory allocations 