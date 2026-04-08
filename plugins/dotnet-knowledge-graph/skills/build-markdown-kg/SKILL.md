---
name: build-markdown-kg
description: >
  Build an RDF knowledge graph from markdown content in a repository. Copilot CLI
  reads markdown files, extracts entities and relations using LLM capabilities,
  and writes JSON-LD and Turtle graph files. Includes ontology setup, entity
  extraction with confidence scoring, SHACL validation, and caching guidance.
  USE FOR: creating a knowledge graph from markdown articles, blog posts, wiki
  pages, or documentation; setting up an ontology with schema.org vocabulary;
  extracting typed entities (people, organizations, software, concepts) and their
  relations from prose. DO NOT USE FOR: querying an existing knowledge graph
  (use query-local-kg instead); building graphs from non-markdown sources (CSV,
  databases, APIs); large-scale production ETL pipelines where a standalone tool
  is more appropriate.
---

# Build a Knowledge Graph from Markdown

Extract entities and relations from markdown content and produce W3C-standard
RDF output (JSON-LD and Turtle) that can be committed to the repository and
queried locally.

## When to Use

- Repository contains markdown content (articles, docs, wiki, blog posts)
- User wants to build a structured, queryable knowledge base from that content
- User wants typed entities with Wikidata links and schema.org vocabulary

## When Not to Use

- Knowledge graph already exists and user wants to query it (use `query-local-kg`)
- Content is not markdown (structured data, databases, APIs)
- User needs a production ETL pipeline (recommend a standalone .NET tool instead)

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Content directory | Yes | Path to markdown files (e.g., `content/`, `docs/`, `_posts/`) |
| Base URL | Yes | Canonical URL prefix for entity IDs (e.g., `https://example.com`) |
| Output directory | No | Where to write graph artifacts (default: `graph/`) |

## Workflow

### Step 1: Discover markdown content

Scan the content directory for `.md` files with YAML frontmatter. Typical
frontmatter fields that help extraction:

```yaml
---
title: "Article Title"
date_published: "2026-04-15"
tags:
  - knowledge-graphs
  - rdf
entity_hints:
  - label: "RDF"
    type: "schema:Thing"
    sameAs: "https://www.wikidata.org/entity/Q54872"
---
```

The `entity_hints` field is optional but improves extraction accuracy by
pre-identifying key entities with their types and external identifiers.

**Checkpoint:** List of markdown files discovered with their frontmatter.

### Step 2: Set up the ontology

Create ontology files in the output directory if they don't exist. The ontology
defines the vocabulary used in the knowledge graph.

**`ontology/context.jsonld`** — JSON-LD context mapping short names to URIs:
```json
{
  "@context": {
    "schema": "https://schema.org/",
    "prov": "http://www.w3.org/ns/prov#",
    "kb": "<base-url>/vocab/kb#",
    "id": "@id",
    "type": "@type",
    "confidence": { "@id": "kb:confidence", "@type": "xsd:decimal" },
    "relatedTo": { "@id": "kb:relatedTo", "@type": "@id" },
    "source": { "@id": "prov:wasDerivedFrom", "@type": "@id" }
  }
}
```

**`ontology/kb.ttl`** — Custom vocabulary extending schema.org:
```turtle
@prefix kb: <<base-url>/vocab/kb#> .
@prefix schema: <https://schema.org/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

kb:confidence a rdf:Property ;
  rdfs:label "confidence" ;
  rdfs:comment "Extractor confidence in [0,1]." ;
  rdfs:range xsd:decimal .

kb:relatedTo a rdf:Property ;
  rdfs:label "related to" ;
  rdfs:comment "Fallback relation when schema.org lacks an appropriate property." ;
  rdfs:domain schema:Thing ;
  rdfs:range schema:Thing .
```

**`ontology/shapes.ttl`** — SHACL validation shapes:
```turtle
@prefix sh: <http://www.w3.org/ns/shacl#> .
@prefix schema: <https://schema.org/> .
@prefix kb: <<base-url>/vocab/kb#> .

<urn:kb:shape:ArticleShape> a sh:NodeShape ;
  sh:targetClass schema:Article ;
  sh:property [ sh:path schema:name ; sh:minCount 1 ] .

<urn:kb:shape:EntityShape> a sh:NodeShape ;
  sh:targetClass schema:Thing ;
  sh:property [ sh:path schema:name ; sh:minCount 1 ] .

<urn:kb:shape:ConfidenceShape> a sh:NodeShape ;
  sh:targetSubjectsOf kb:confidence ;
  sh:property [ sh:path kb:confidence ; sh:minInclusive 0.0 ; sh:maxInclusive 1.0 ] .
```

**Checkpoint:** Ontology files created in `<output-dir>/ontology/`.

### Step 3: Extract entities and relations

For each markdown file, read the content and extract structured data. Use the
entity extraction guidelines below.

**What to extract:**

1. **Entities** — Named things mentioned in the text:
   - People (`schema:Person`)
   - Organizations (`schema:Organization`)
   - Software tools and frameworks (`schema:SoftwareApplication`)
   - Publications or creative works (`schema:CreativeWork`)
   - General concepts (`schema:Thing`)

2. **Relations** — How entities connect:
   - `schema:mentions` — Article mentions an entity
   - `schema:about` — Article's primary topic
   - `schema:author` / `schema:creator` — Authorship
   - `schema:sameAs` — Link to Wikidata or external URI
   - `kb:relatedTo` — Fallback for relations that don't fit schema.org

3. **Confidence scores** — How explicitly the text states the relation:
   - 0.9+: Directly stated ("X created Y")
   - 0.7–0.9: Strongly implied
   - 0.5–0.7: Weakly implied
   - Below 0.5: Do not emit

**Entity ID conventions:**
- Articles: `<base-url>/<path-slug>/` (from file path or canonical URL)
- Entities: `<base-url>/id/<slug>` where slug = lowercase, hyphens, no special chars

**Per-article output:** For each article, produce a JSON-LD file:

```json
{
  "@context": "../../ontology/context.jsonld",
  "@graph": [
    {
      "id": "<article-url>",
      "type": "schema:Article",
      "schema:name": "Article Title",
      "schema:datePublished": "2026-04-15",
      "schema:keywords": "tag1, tag2",
      "schema:mentions": ["<base-url>/id/entity-slug"]
    },
    {
      "id": "<base-url>/id/entity-slug",
      "type": "schema:SoftwareApplication",
      "schema:name": "Entity Name",
      "schema:sameAs": "https://www.wikidata.org/entity/Q12345"
    }
  ]
}
```

**Checkpoint:** JSON-LD files written to `<output-dir>/articles/`.

### Step 4: Generate Turtle output

For each JSON-LD file, also produce a Turtle (`.ttl`) file. Turtle is more
compact and human-readable:

```turtle
@prefix schema: <https://schema.org/> .
@prefix kb: <<base-url>/vocab/kb#> .

<article-url> a schema:Article ;
  schema:name "Article Title" ;
  schema:datePublished "2026-04-15"^^xsd:date ;
  schema:mentions <base-url/id/entity-slug> .

<base-url/id/entity-slug> a schema:SoftwareApplication ;
  schema:name "Entity Name" ;
  schema:sameAs <https://www.wikidata.org/entity/Q12345> .
```

**Checkpoint:** Turtle files written alongside JSON-LD files.

### Step 5: Validate

Check the output for common issues:
- Every entity has a `schema:name`
- Every article has a `schema:name` and `schema:datePublished`
- Confidence values are in [0, 1]
- No duplicate entity IDs with conflicting types
- `schema:sameAs` links point to valid external URIs

### Step 6: Commit artifacts

The graph artifacts should be committed to the repository:

```
git add graph/
git commit -m "Build knowledge graph from markdown content"
```

This makes the knowledge graph queryable by Copilot CLI in future sessions
using the `query-local-kg` skill.

## Caching Guidance

For repositories with many articles, extraction can be slow. Implement caching
to avoid re-extracting unchanged content:

- Cache key: content hash + extraction prompt version
- Cache location: `<output-dir>/cache/<slug>.<version>.json`
- On subsequent builds, check cache before extracting
- Only extract new or changed articles

This is especially important when using LLM APIs with rate limits (e.g.,
GitHub Models free tier: 150 requests/day).

## Validation

- [ ] JSON-LD files are valid JSON with `@context` and `@graph`
- [ ] Every entity has an `id`, `type`, and `schema:name`
- [ ] Entity IDs follow the `<base-url>/id/<slug>` convention
- [ ] Turtle files parse without syntax errors
- [ ] Confidence scores are in the range [0, 1]
- [ ] No duplicate entity IDs with conflicting types

## Common Pitfalls

| Trap | Solution |
|------|----------|
| LLM invents entities not in the text | Only extract entities explicitly stated or strongly implied |
| Entity type inconsistency (e.g., "React" as Person) | Use entity_hints in frontmatter to pre-specify types |
| Duplicate entities with slight name variations | Canonicalize by slug: "JSON-LD" and "json-ld" → same entity |
| Missing sameAs links | Use web search to find Wikidata URIs for well-known entities |
| Large repos overwhelm LLM context | Process one article at a time; cache results |
| Turtle syntax errors from special characters | Escape quotes and backslashes in string literals |
