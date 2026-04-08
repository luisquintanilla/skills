---
name: query-local-kg
description: >
  Query a local RDF knowledge graph (Turtle .ttl or JSON-LD .jsonld files).
  Read graph files, understand RDF triples and relationships (types, mentions,
  authorship), and answer questions about entities, articles, and connections.
  USE FOR: answering questions about entities, relationships, or content in a
  local knowledge graph; exploring graph contents; finding connections between
  articles and entities; understanding Turtle or JSON-LD graph files.
  DO NOT USE FOR: building a knowledge graph (use build-markdown-kg); querying
  remote SPARQL endpoints.
---

# Query a Local Knowledge Graph

Answer questions about a knowledge graph consisting of RDF files (Turtle `.ttl`
or JSON-LD `.jsonld`) committed to the repository.

## When to Use

- User asks about entities, topics, or relationships in their knowledge graph
- User wants to explore what a knowledge graph contains
- Graph files (`.ttl`, `.jsonld`) exist in the repository

## When Not to Use

- No graph files exist yet → use `build-markdown-kg` first
- User wants to build/extract a knowledge graph from markdown

## Workflow

### Step 1: Read the graph files

Read the Turtle or JSON-LD files with `view`. They are typically under a
`graph/` directory.

**How to read Turtle (`.ttl`):**

Turtle encodes RDF triples — `subject predicate object` statements. Semicolons
continue with the same subject:

```turtle
<https://example.com/article-1/> a schema:Article ;     # this resource IS an Article
  schema:name "My Article" ;                             # it HAS name "My Article"
  schema:mentions <https://example.com/id/sparql> .      # it MENTIONS the entity sparql
```

**How to read JSON-LD (`.jsonld`):**

JSON-LD files contain an `@graph` array of nodes:

```json
{
  "@graph": [
    { "id": "...", "type": "schema:Article", "schema:name": "My Article",
      "schema:mentions": ["https://example.com/id/sparql"] },
    { "id": "...", "type": "schema:Thing", "schema:name": "SPARQL" }
  ]
}
```

**Key vocabulary** (schema.org + custom `kb:` namespace):

| Predicate | Meaning |
|-----------|---------|
| `a` / `type` | RDF type — Article, Person, Organization, SoftwareApplication, Thing |
| `schema:name` | Display name or title |
| `schema:mentions` | An article mentions this entity |
| `schema:about` | An article's primary topic |
| `schema:hasPart` | A container includes this item (plugin → skill, collection → member) |
| `schema:isPartOf` | This item belongs to a container (skill → plugin) |
| `schema:author` | Who wrote the article |
| `schema:creator` | Who created a technology or entity |
| `schema:datePublished` | Publication date |
| `schema:sameAs` | Link to external identifier (e.g., Wikidata) |
| `schema:keywords` | Comma-separated tags |
| `kb:relatedTo` | Generic relationship between entities (chains, alternatives, prerequisites) |

### Step 2: Answer the question

Interpret the RDF triples to answer the user's question. All resources in the
graph — articles, people, software, organizations, and other entities — are
first-class nodes that can be listed, filtered, and connected.

Common patterns:

- **"What's in the graph?"** → List all resources with their types and names
- **"Which articles mention X?"** → Follow `schema:mentions` links from articles
  to find those referencing entity X by name
- **"Find all people"** → Filter resources by their RDF type (`a schema:Person`)
- **"What topics appear across articles?"** → Collect `schema:mentions` targets
  and find which entities are mentioned by multiple articles
- **"Who authored articles about X?"** → Follow `schema:author` from articles
  that `schema:mentions` entity X
- **"What skills should I use for task X?"** → Find resources that `schema:mentions`
  the relevant technology, check `schema:isPartOf` for their container, then follow
  `kb:relatedTo` for workflow chains and prerequisites
- **"How is X connected to Y?"** → Trace chains: entity → `kb:relatedTo` → entity →
  `schema:hasPart` → member → `schema:mentions` → technology

Present findings conversationally with specific data from the graph.

### Step 3: For large graphs — use SPARQL

If graph files exceed ~100KB total, generate a SPARQL query and execute it with
the included dotNetRDF script. See `references/schema-reference.md` for the full
vocabulary and SPARQL patterns.

```powershell
dotnet run scripts/execute-sparql.cs -- "<graph-directory>" "<sparql-query>"
```

Use only `SELECT` or `ASK` queries. Include `LIMIT` clauses. Match names
case-insensitively: `FILTER(CONTAINS(LCASE(STR(?name)), "search term"))`.

## Validation

- [ ] User's question answered with specific data from the graph files
- [ ] All stated facts match the actual graph content
- [ ] No invented entities or relationships
