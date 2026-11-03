# KIP (AI Q&A) — Status

**Date verified:** 2026-06-03  
**Audit finding:** Stub only — returns hardcoded string (Phase 5 audit)

---

## Current state

`KipController.cs` (`/api/kip/query`) is a **placeholder**:

```csharp
[HttpPost("query")]
public ActionResult<KipQueryResponse> Query([FromBody] KipQueryRequest request)
{
    return Ok(new KipQueryResponse("KIP is not yet active. Check back soon."));
}
```

It accepts the correct request shape (`KipQueryRequest` with context + message) but
performs no AI inference, no vector search, and no document retrieval. Every call
returns the same literal string regardless of input.

---

## What IS built (document pipeline)

The document upload and processing pipeline is fully implemented:

| Component | File | Status |
|-----------|------|--------|
| Upload endpoint | `KnowledgeDocumentsController.cs` `/knowledge/documents/upload` | ✅ Working |
| Chunking service | `IKnowledgeDocumentProcessingService` | ✅ Implemented |
| `KnowledgeDocumentChunk` entity | `Ecosys.Domain.Entities` | ✅ In schema |
| List documents | `GET /knowledge/documents` | ✅ Working |
| Delete document | `DELETE /knowledge/documents/{id}` | ✅ Working |
| Frontend upload UI | `KnowledgeCentrePage.tsx` | ✅ Working |
| Frontend KIP button/panel | `KipButton.tsx`, `KipPanel.tsx` | ✅ Rendered (in Reports page) |
| `kipService.ts` | `src/services/kipService.ts` | ✅ Calls `/api/kip/query` |

---

## What is NOT built

- Vector embedding of chunks (no embedding model integration)
- Semantic similarity search over `KnowledgeDocumentChunk`
- LLM inference call (no OpenAI / Azure OpenAI / Anthropic integration)
- Source attribution in responses
- Context injection from uploaded documents into the query

---

## Recommended Phase 2 implementation path

1. Choose an embedding provider (e.g. OpenAI `text-embedding-3-small` or Azure OpenAI).
2. On document upload, generate embeddings for each chunk and store them (add a vector
   column to `knowledge_document_chunks`, or use `pgvector`).
3. On `/api/kip/query`, embed the incoming `Message`, run a similarity search over
   chunks scoped to `TenantId`, inject the top-k chunks as context, call the LLM
   with a system prompt, and return the grounded response.
4. The `KipQueryRequest.Context` fields (`Screen`, `EntityType`, `EntityId`) are
   already designed for workspace-aware grounding — use them to filter relevant docs.

---

## Risk if released as-is

Low — the stub response is honest ("KIP is not yet active"). The UI's KipPanel renders
the response text as-is so users will see the stub message. No data is leaked, no
errors thrown. Safe to ship to Phase 1 tenants; just do not advertise AI Q&A as live.
