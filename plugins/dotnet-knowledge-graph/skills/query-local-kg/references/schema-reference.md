# Schema Reference for Knowledge Graph Queries

This document describes the vocabulary used in knowledge graphs built from
markdown content. Use it to understand the available classes, properties, and
query patterns when answering questions about a knowledge graph.

## Namespace Prefixes

| Prefix | URI | Purpose |
|--------|-----|---------|
| `schema:` | `https://schema.org/` | Primary vocabulary for entities, articles, and relations |
| `kb:` | Varies per project (e.g., `https://example.com/vocab/kb#`) | Custom properties for extraction metadata |
| `prov:` | `http://www.w3.org/ns/prov#` | Provenance (which chunk produced an assertion) |
| `rdf:` | `http://www.w3.org/1999/02/22-rdf-syntax-ns#` | RDF type system |
| `xsd:` | `http://www.w3.org/2001/XMLSchema#` | Datatype definitions |

## Entity Types (Classes)

| Class | Description | Example |
|-------|-------------|---------|
| `schema:Article` | A markdown article or entry | A blog post, documentation page, wiki entry |
| `schema:Person` | A person mentioned in content | "Ada Lovelace", "Linus Torvalds" |
| `schema:Organization` | An organization | "Microsoft", "W3C", "CNCF" |
| `schema:SoftwareApplication` | A software tool or framework | "dotNetRDF", "React", "Kubernetes" |
| `schema:CreativeWork` | A publication, specification, or creative work | "RFC 7231", "The Art of Computer Programming" |
| `schema:Thing` | Generic fallback for entities that don't fit above | "REST API", "microservices", "machine learning" |

## Properties

### Article Properties

| Property | Range | Description |
|----------|-------|-------------|
| `schema:name` | `xsd:string` | Title of the article |
| `schema:datePublished` | `xsd:date` | Publication date |
| `schema:dateModified` | `xsd:date` | Last modified date |
| `schema:keywords` | `xsd:string` | Comma-separated tags |
| `schema:description` | `xsd:string` | Summary text |
| `schema:author` | `schema:Person` | Author of the article |
| `schema:mentions` | `schema:Thing` | Entity mentioned in the article |
| `schema:about` | `schema:Thing` | Primary topic of the article |

### Entity Properties

| Property | Range | Description |
|----------|-------|-------------|
| `schema:name` | `xsd:string` | Label/name of the entity |
| `schema:sameAs` | URI | Link to external identifier (e.g., Wikidata) |
| `schema:creator` | `schema:Person` or `schema:Organization` | Creator of the entity |

### Extraction Metadata

| Property | Range | Description |
|----------|-------|-------------|
| `kb:relatedTo` | `schema:Thing` | Fallback relation when no schema.org property fits |
| `kb:confidence` | `xsd:decimal` (0.0–1.0) | How explicitly the text states the relation |
| `prov:wasDerivedFrom` | URI | Source chunk that produced the assertion |

## Entity ID Conventions

- **Articles**: Canonical URL (e.g., `https://example.com/2026/04/my-article/`)
- **Entities**: Base URL + `/id/` + slug (e.g., `https://example.com/id/dotnetrdf`)
- **Slugs**: Lowercase, hyphens, no special characters (`"JSON-LD 1.1"` → `json-ld-1-1`)

## JSON-LD Structure

Each article produces a JSON-LD file with an `@graph` array:

```json
{
  "@context": "../../ontology/context.jsonld",
  "@graph": [
    {
      "id": "https://example.com/2026/04/my-article/",
      "type": "schema:Article",
      "schema:name": "My Article Title",
      "schema:datePublished": "2026-04-15",
      "schema:keywords": "knowledge-graphs, rdf",
      "schema:mentions": ["https://example.com/id/sparql", "https://example.com/id/rdf"]
    },
    {
      "id": "https://example.com/id/sparql",
      "type": "schema:Thing",
      "schema:name": "SPARQL",
      "schema:sameAs": "https://www.wikidata.org/entity/Q54872"
    }
  ]
}
```

## Confidence Scoring

Extraction confidence indicates how explicitly the source text states a relation:
- **0.9+**: Directly stated ("X created Y")
- **0.7–0.9**: Strongly implied
- **0.5–0.7**: Weakly implied
- **< 0.5**: Not emitted (below threshold)
