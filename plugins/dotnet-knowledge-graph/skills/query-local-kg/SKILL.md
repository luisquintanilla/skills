---
name: query-local-kg
description: >
  Query a local RDF knowledge graph built from markdown content. Copilot CLI reads
  JSON-LD or Turtle graph files directly and answers questions about entities,
  relations, and articles — no SPARQL endpoint needed. For large graphs that exceed
  context limits, optionally generate and execute SPARQL queries via a dotNetRDF
  file-based app. USE FOR: answering questions about entities, relations, and content
  in a local knowledge graph; exploring what a knowledge graph contains; finding
  connections between articles and entities. DO NOT USE FOR: building or extracting
  a knowledge graph from markdown (use build-markdown-kg instead); querying remote
  SPARQL endpoints; general-purpose RDF/OWL ontology engineering.
---

# Query a Local Knowledge Graph

Answer questions about a knowledge graph built from markdown content. The graph
consists of JSON-LD and/or Turtle files committed to the repository, typically
under a `graph/` directory.

## When to Use

- User asks questions about entities, topics, or relationships in their content
- User wants to explore what their knowledge graph contains
- User wants to find connections between articles and entities
- Graph files (`.jsonld`, `.ttl`) exist in the repository

## When Not to Use

- No graph files exist yet (use `build-markdown-kg` to create them first)
- User wants to build/extract a knowledge graph from markdown
- User needs to query a remote SPARQL endpoint or external triple store

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Graph directory | Yes | Path to directory containing `.jsonld` or `.ttl` files (e.g., `graph/articles/`) |
| Ontology/schema | No | Path to `context.jsonld` or vocabulary files; defaults to schema.org conventions |
| User question | Yes | Natural language question about the knowledge graph |

## Workflow

### Step 1: Locate the graph files

Find JSON-LD or Turtle files in the repository:

```
graph/
  articles/
    *.jsonld          # Per-article JSON-LD
    *.ttl             # Per-article Turtle
  views/
    entities.json     # Optional precomputed views
```

Check the graph size to choose the right query approach:
- **Small graph** (total files < ~100KB): Read files directly with `view` — proceed to Step 2a
- **Large graph** (total files > ~100KB): Generate and execute SPARQL — proceed to Step 2b

### Step 2a: Direct read (small graphs)

Read the JSON-LD or Turtle files directly. JSON-LD files are valid JSON and
easy to process:

```json
{
  "@context": "../../ontology/context.jsonld",
  "@graph": [
    { "id": "https://example.com/article-slug/", "type": "schema:Article", "schema:name": "Article Title" },
    { "id": "https://example.com/id/some-entity", "type": "schema:SoftwareApplication", "schema:name": "Some Entity" }
  ]
}
```

Scan the `@graph` arrays across files to answer the user's question. Look for:
- **Entities**: Objects with `type` other than `schema:Article`
- **Articles**: Objects with `type` of `schema:Article`
- **Mentions**: `schema:mentions` links from articles to entities
- **Relations**: `schema:about`, `schema:author`, `schema:creator`, `kb:relatedTo`
- **External links**: `schema:sameAs` pointing to Wikidata or other URIs

Present results conversationally. Include entity types and counts where helpful.

**Checkpoint:** Results presented and user question answered.

### Step 2b: SPARQL execution (large graphs)

When the graph is too large to read into context, generate a SPARQL query and
execute it using a .NET 10 file-based app with dotNetRDF.

#### Generate SPARQL

Use the schema reference below to generate valid SPARQL. The knowledge graph
uses schema.org vocabulary with a custom `kb:` namespace.

**Available prefixes:**
```sparql
PREFIX schema: <https://schema.org/>
PREFIX kb:     <https://example.com/vocab/kb#>
PREFIX prov:   <http://www.w3.org/ns/prov#>
PREFIX rdf:    <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX xsd:    <http://www.w3.org/2001/XMLSchema#>
```

**Available classes:**
- `schema:Article` — A markdown article/entry
- `schema:Person` — A person mentioned in content
- `schema:Organization` — An organization
- `schema:SoftwareApplication` — A software tool or framework
- `schema:CreativeWork` — A creative work or publication
- `schema:Thing` — Generic fallback entity type

**Available properties:**
- `schema:name` — Label/title of any entity or article (`xsd:string`)
- `schema:mentions` — Article mentions an entity (`Article → Thing`)
- `schema:about` — Article is about a topic (`Article → Thing`)
- `schema:author` — Author of an article (`Article → Person`)
- `schema:creator` — Creator of a thing (`Thing → Person/Org`)
- `schema:datePublished` — Publication date (`Article → xsd:date`)
- `schema:sameAs` — Link to external URI (e.g., Wikidata)
- `schema:keywords` — Comma-separated tags (`Article → xsd:string`)
- `schema:description` — Summary text (`Article → xsd:string`)
- `kb:relatedTo` — Fallback relation (`Thing → Thing`)
- `kb:confidence` — Extraction confidence 0..1 (`xsd:decimal`)

**Query generation rules:**
1. Only use classes and properties listed above — do not invent predicates
2. Only generate `SELECT` or `ASK` queries — never `INSERT`, `DELETE`, or `UPDATE`
3. Include a `LIMIT` clause (default 100) unless the user asks for all results
4. Match entity names case-insensitively: `FILTER(CONTAINS(LCASE(STR(?name)), "search term"))`
5. Articles are typed `schema:Article`; entities are `schema:Thing` or a subtype

**Common patterns:**

Find all entities:
```sparql
PREFIX schema: <https://schema.org/>
SELECT DISTINCT ?entity ?name ?type WHERE {
  ?entity a ?type ; schema:name ?name .
  FILTER(?type != schema:Article)
} LIMIT 100
```

Find articles mentioning an entity:
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ; schema:name ?title ; schema:mentions ?entity .
  ?entity schema:name ?entityName .
  FILTER(CONTAINS(LCASE(STR(?entityName)), "search term"))
} LIMIT 100
```

Find connections between entities:
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?subject ?predicate ?object WHERE {
  ?subject ?predicate ?object .
  FILTER(?predicate != rdf:type)
} LIMIT 50
```

Find entities by type:
```sparql
PREFIX schema: <https://schema.org/>
SELECT ?entity ?name WHERE {
  ?entity a schema:Person ; schema:name ?name .
} LIMIT 100
```

#### Execute SPARQL

Run the query against local `.ttl` files using the `execute-sparql.cs` file-based
app included in this skill's `scripts/` directory:

```powershell
dotnet run scripts/execute-sparql.cs -- "<graph-directory>" "<sparql-query>"
```

The script loads all `.ttl` files from the directory into an in-memory triple
store, executes the SPARQL query, and outputs results as JSON to stdout.

If the query has a syntax error, read the error message, fix the SPARQL, and
re-execute. This retry loop is expected — SPARQL syntax can be tricky.

**Checkpoint:** SPARQL results received as JSON.

### Step 3: Interpret and present results

Read the query results (whether from direct file reading or SPARQL execution)
and present them conversationally:

- Summarize the findings in natural language
- Include counts where relevant ("Found 12 entities of type Person")
- Offer follow-up suggestions ("Would you like to see which articles mention these entities?")
- If results are empty, suggest broadening the search or checking entity names

## Validation

- [ ] User's question is answered with specific data from the graph
- [ ] Entity names and types are accurate (match what's in the graph files)
- [ ] If SPARQL was used, the query is syntactically valid and uses only documented predicates

## Common Pitfalls

| Trap | Solution |
|------|----------|
| Graph files don't exist yet | Direct user to `build-markdown-kg` skill first |
| SPARQL uses predicates not in the ontology | Stick to the schema reference above; use `kb:relatedTo` as fallback |
| Case-sensitive name matching returns no results | Always use `FILTER(CONTAINS(LCASE(STR(?name)), "..."))` |
| JSON-LD `@context` uses relative paths | Resolve relative to the file location when reading |
| Turtle file has syntax errors | Validate with dotNetRDF before querying; check for unescaped quotes |
