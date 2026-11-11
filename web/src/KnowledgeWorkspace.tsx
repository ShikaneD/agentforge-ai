import { useState, type FormEvent } from 'react'
import { ingestKnowledgeDocument, searchKnowledge } from './api'
import type { KnowledgeDocumentResponse, KnowledgeSearchResult } from './types'

export function KnowledgeWorkspace() {
  const [name, setName] = useState('')
  const [content, setContent] = useState('')
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<KnowledgeSearchResult[]>([])
  const [document, setDocument] = useState<KnowledgeDocumentResponse | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const created = await ingestKnowledgeDocument(name, content)
      setDocument(created)
      setName('')
      setContent('')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not ingest document.')
    } finally {
      setBusy(false)
    }
  }

  async function find(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      setResults(await searchKnowledge(query))
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Knowledge search failed.')
    } finally {
      setBusy(false)
    }
  }

  async function loadFile(file: File | undefined) {
    if (!file) return
    if (file.size > 1_000_000) {
      setError('Choose a text file smaller than 1 MB.')
      return
    }
    setError('')
    setName(file.name)
    setContent(await file.text())
  }

  return (
    <section className="knowledge-workspace">
      <div className="panel knowledge-ingest">
        <div className="panel-heading"><div><h2>Add a knowledge source</h2><p>Ingest plain text, Markdown, or CSV for retrieval-augmented chat.</p></div><span className="knowledge-mark">◇</span></div>
        <form className="knowledge-form" onSubmit={(event) => void upload(event)}>
          <label>Document name<input maxLength={200} onChange={(event) => setName(event.target.value)} placeholder="Project notes" required value={name} /></label>
          <label>Plain-text content<textarea maxLength={1_000_000} onChange={(event) => setContent(event.target.value)} placeholder="Paste text here or choose a .txt, .md, or .csv file…" required rows={8} value={content} /></label>
          <div className="knowledge-actions">
            <label className="file-picker">Choose text file<input accept=".txt,.md,.csv,text/plain,text/markdown,text/csv" onChange={(event) => void loadFile(event.target.files?.[0])} type="file" /></label>
            <button className="primary-button" disabled={busy || !name.trim() || !content.trim()} type="submit">{busy ? 'Processing…' : 'Ingest document'}</button>
          </div>
        </form>
        {document && <p className="success-message" role="status">Added “{document.name}” as {document.chunkCount} searchable chunks.</p>}
      </div>

      <div className="panel knowledge-search">
        <div className="panel-heading"><div><h2>Search your knowledge</h2><p>Preview the context available to your agents.</p></div></div>
        <form className="search-form" onSubmit={(event) => void find(event)}>
          <input aria-label="Search knowledge" maxLength={500} onChange={(event) => setQuery(event.target.value)} placeholder="Search documents…" required value={query} />
          <button className="primary-button" disabled={busy || !query.trim()} type="submit">Search</button>
        </form>
        {error && <p className="chat-error" role="alert">{error}</p>}
        {results.length === 0 ? <p className="subtle-empty">Search results will appear here.</p> : (
          <div className="knowledge-results">{results.map((result, index) => (
            <article className="knowledge-result" key={`${result.source}-${index}`}><strong>{result.source}</strong><p>{result.content}</p></article>
          ))}</div>
        )}
      </div>
    </section>
  )
}
