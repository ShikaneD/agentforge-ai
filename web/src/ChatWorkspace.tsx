import { useEffect, useRef, useState, type FormEvent } from 'react'
import { getConversation, sendChat } from './api'
import type { Agent, Conversation } from './types'

interface ChatWorkspaceProps {
  agents: Agent[]
  conversations: Conversation[]
  onConversationCreated: (conversation: Conversation) => void
}

export function ChatWorkspace({ agents, conversations, onConversationCreated }: ChatWorkspaceProps) {
  const [conversationId, setConversationId] = useState('')
  const [agentId, setAgentId] = useState('')
  const [messages, setMessages] = useState<Conversation['messages']>([])
  const [draft, setDraft] = useState('')
  const [loadingConversation, setLoadingConversation] = useState(false)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')
  const endOfMessages = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endOfMessages.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages, sending])

  async function selectConversation(conversation: Conversation) {
    setConversationId(conversation.id)
    if (conversation.agentId) setAgentId(conversation.agentId)
    setLoadingConversation(true)
    setError('')
    try {
      const fullConversation = await getConversation(conversation.id)
      setMessages(fullConversation.messages)
      if (fullConversation.agentId) setAgentId(fullConversation.agentId)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not load conversation.')
    } finally {
      setLoadingConversation(false)
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const message = draft.trim()
    if (!message || sending) return

    setError('')
    setSending(true)
    try {
      const response = await sendChat({
        ...(conversationId ? { conversationId } : {}),
        ...(agentId ? { agentId } : {}),
        message,
      })
      const now = new Date().toISOString()
      setConversationId(response.conversationId)
      setAgentId(response.agentId)
      setMessages((current) => [
        ...current,
        { id: crypto.randomUUID(), role: 'user', content: message, createdAtUtc: now },
        { id: crypto.randomUUID(), role: 'assistant', content: response.assistantMessage, sources: response.sources, createdAtUtc: response.createdAtUtc },
      ])
      onConversationCreated({
        id: response.conversationId,
        agentId: response.agentId,
        title: message.slice(0, 80),
        messages: [],
        updatedAtUtc: response.createdAtUtc,
      })
      setDraft('')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not send this message.')
    } finally {
      setSending(false)
    }
  }

  function startNewConversation() {
    setConversationId('')
    setMessages([])
    setError('')
  }

  return (
    <section className="chat-workspace">
      <aside className="chat-sidebar panel">
        <div className="chat-sidebar-heading">
          <div><h2>Conversations</h2><p>Your recent agent sessions</p></div>
          <button className="secondary-button" onClick={startNewConversation} type="button">＋ New</button>
        </div>
        <div className="conversation-list">
          {conversations.length === 0 ? (
            <p className="subtle-empty">Your conversations will appear here.</p>
          ) : conversations.map((conversation) => (
            <button
              className={`conversation-item ${conversation.id === conversationId ? 'active' : ''}`}
              key={conversation.id}
              onClick={() => void selectConversation(conversation)}
              type="button"
            >
              <span className="conversation-item-icon">◷</span>
              <span><strong>{conversation.title || 'New conversation'}</strong><small>{new Date(conversation.updatedAtUtc).toLocaleDateString()}</small></span>
            </button>
          ))}
        </div>
      </aside>

      <div className="chat-panel panel">
        <header className="chat-header">
          <div className="chat-header-copy"><span className="chat-header-mark">✳</span><span><strong>AgentForge assistant</strong><small>Choose an agent to start a conversation</small></span></div>
          <label className="agent-picker">Agent
            <select value={agentId} onChange={(event) => setAgentId(event.target.value)}>
              <option value="">Auto-select</option>
              {agents.filter((agent) => agent.isEnabled).map((agent) => (
                <option key={agent.id} value={agent.id}>{agent.name}</option>
              ))}
            </select>
          </label>
        </header>

        <div className="chat-messages" aria-live="polite">
          {loadingConversation ? (
            <div className="loading-state"><span className="spinner" /> Loading conversation…</div>
          ) : messages.length === 0 ? (
            <div className="chat-welcome">
              <span className="chat-welcome-icon">✦</span>
              <h2>What would you like to work on?</h2>
              <p>Ask a question, explore your knowledge, or delegate a task to one of your agents.</p>
              <div className="suggestion-list">
                <button onClick={() => setDraft('Summarize what my agents can help me with.')} type="button">What can my agents do?</button>
                <button onClick={() => setDraft('Search my knowledge sources for the latest project notes.')} type="button">Search my knowledge</button>
              </div>
            </div>
          ) : messages.map((message) => (
            <article className={`chat-message ${message.role === 'assistant' ? 'assistant' : 'user'}`} key={message.id}>
              <span className="message-avatar">{message.role === 'assistant' ? 'A' : 'You'}</span>
              <div className="message-body">
                <div className="message-meta"><strong>{message.role === 'assistant' ? 'AgentForge' : 'You'}</strong><time>{new Date(message.createdAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</time></div>
                <p>{message.content}</p>
                {message.sources && message.sources.length > 0 && (
                  <div className="message-sources">Sources: {message.sources.join(', ')}</div>
                )}
              </div>
            </article>
          ))}
          {sending && <div className="chat-thinking"><span className="spinner" /> Agent is thinking…</div>}
          <div ref={endOfMessages} />
        </div>

        {error && <div className="chat-error" role="alert">{error}</div>}
        <form className="chat-composer" onSubmit={(event) => void handleSubmit(event)}>
          <textarea
            aria-label="Message"
            maxLength={12000}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault()
                event.currentTarget.form?.requestSubmit()
              }
            }}
            placeholder="Message your agent…"
            rows={2}
            value={draft}
          />
          <div className="composer-footer"><span>AI responses may be inaccurate. Review important information.</span><button className="primary-button" disabled={!draft.trim() || sending || loadingConversation} type="submit">{sending ? 'Working…' : 'Send'} <span aria-hidden="true">↑</span></button></div>
        </form>
      </div>
    </section>
  )
}
