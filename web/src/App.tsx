import { useEffect, useState, type FormEvent } from 'react'
import { createAgent, getAgents, getConversations, getHealth, getProviders } from './api'
import type { Agent, AgentRole, Conversation, ModelProvider } from './types'
import { ChatWorkspace } from './ChatWorkspace'
import { KnowledgeWorkspace } from './KnowledgeWorkspace'
import './App.css'

const roleNames: Record<AgentRole, string> = {
  Knowledge: 'Knowledge',
  Developer: 'Developer',
  Analytics: 'Analytics',
  Validation: 'Validation',
}

const roleStyles: Record<AgentRole, string> = {
  Knowledge: 'violet',
  Developer: 'blue',
  Analytics: 'orange',
  Validation: 'green',
}

function App({ displayName = 'Developer' }: { displayName?: string }) {
  const [agents, setAgents] = useState<Agent[]>([])
  const [providers, setProviders] = useState<ModelProvider[]>([])
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [apiOnline, setApiOnline] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [activeSection, setActiveSection] = useState('Overview')

  useEffect(() => {
    let active = true

    async function loadDashboard() {
      setLoading(true)
      setError('')
      const results = await Promise.allSettled([
        getAgents(),
        getProviders(),
        getConversations(),
        getHealth(),
      ])

      if (!active) return

      const [agentResult, providerResult, conversationResult, healthResult] = results
      if (agentResult.status === 'fulfilled') setAgents(agentResult.value)
      if (providerResult.status === 'fulfilled') setProviders(providerResult.value)
      if (conversationResult.status === 'fulfilled') setConversations(conversationResult.value)
      setApiOnline(healthResult.status === 'fulfilled')

      const failedRequests = results.filter((result) => result.status === 'rejected')
      if (failedRequests.length > 0) {
        const firstFailure = failedRequests[0]
        setError(firstFailure.status === 'rejected' ? firstFailure.reason.message : '')
      }

      setLoading(false)
    }

    void loadDashboard()
    return () => {
      active = false
    }
  }, [])

  async function handleCreateAgent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const formData = new FormData(form)
    const name = String(formData.get('name') ?? '').trim()
    const description = String(formData.get('description') ?? '').trim()
    const role = String(formData.get('role')) as AgentRole
    const modelProvider = String(formData.get('provider') ?? '')

    setIsSaving(true)
    setError('')
    try {
      const agent = await createAgent({ name, description, role, modelProvider, isEnabled: true })
      setAgents((current) => [...current, agent])
      setIsCreateOpen(false)
      form.reset()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not create agent.')
    } finally {
      setIsSaving(false)
    }
  }

  const enabledAgentCount = agents.filter((agent) => agent.isEnabled).length
  const sections = ['Overview', 'Agents', 'Conversations', 'Knowledge', 'Providers']
  const pageDescriptions: Record<string, string> = {
    Overview: 'A clear view of your agents and workspace activity.',
    Agents: 'Configure and manage your AI specialists.',
    Conversations: 'Work with your agents and keep task context in one place.',
    Knowledge: 'Add trusted reference material for grounded agent responses.',
    Providers: 'Choose the model services your agents can use.',
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="#" aria-label="AgentForge home" onClick={() => setActiveSection('Overview')}>
          <span className="brand-mark" aria-hidden="true">A</span>
          <span>agent<span className="brand-accent">forge</span></span>
        </a>

        <div className="workspace-label">WORKSPACE</div>
        <nav className="main-nav" aria-label="Main navigation">
          {sections.map((section, index) => (
            <button
              className={`nav-item ${activeSection === section ? 'selected' : ''}`}
              key={section}
              onClick={() => setActiveSection(section)}
              type="button"
            >
              <span className="nav-icon" aria-hidden="true">{['◫', '✳', '◷', '▤', '◇'][index]}</span>
              {section}
              {section === 'Agents' && <span className="nav-count">{agents.length}</span>}
            </button>
          ))}
        </nav>

        <div className="sidebar-bottom">
          <div className="sidebar-status">
            <span className={`status-dot ${apiOnline ? 'online' : ''}`} />
            <span>{apiOnline ? 'API connected' : 'API disconnected'}</span>
          </div>
          <div className="profile">
            <span className="avatar">DS</span>
            <span className="profile-copy"><strong>{displayName}</strong><small>Workspace member</small></span>
            <span className="profile-menu" aria-hidden="true">···</span>
          </div>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumbs"><span>Workspace</span><span className="crumb-separator">/</span><strong>{activeSection}</strong></div>
          <div className="topbar-actions">
            <span className="environment-tag"><span className="status-dot online" /> Development</span>
            <button className="icon-button" aria-label="Notifications" type="button">♧</button>
            <span className="topbar-avatar">DS</span>
          </div>
        </header>

        <div className="page-content">
          <section className="page-heading">
            <div>
              <p className="eyebrow">YOUR AI WORKSPACE</p>
              <h1>{activeSection === 'Overview' ? `Good morning, ${displayName}` : activeSection} <span className="wave" aria-hidden="true">✦</span></h1>
              <p className="page-subtitle">{pageDescriptions[activeSection]}</p>
            </div>
            {activeSection !== 'Conversations' && activeSection !== 'Knowledge' && (
              <button className="primary-button" onClick={() => setIsCreateOpen(true)} type="button">
                <span aria-hidden="true">＋</span> Create agent
              </button>
            )}
          </section>

          {activeSection === 'Conversations' ? (
            <ChatWorkspace
              agents={agents}
              conversations={conversations}
              onConversationCreated={(conversation) => setConversations((current) => [
                conversation,
                ...current.filter((item) => item.id !== conversation.id),
              ])}
            />
          ) : activeSection === 'Knowledge' ? (
            <KnowledgeWorkspace />
          ) : (
            <>
          {error && (
            <div className="error-banner" role="alert">
              <span><strong>Some workspace data could not be loaded.</strong> {error}</span>
              <button onClick={() => window.location.reload()} type="button">Try again</button>
            </div>
          )}

          <section className="metrics-grid" aria-label="Workspace summary">
            <MetricCard label="Total agents" value={loading ? '—' : agents.length} detail="Configured in workspace" icon="✳" tone="violet" />
            <MetricCard label="Active agents" value={loading ? '—' : enabledAgentCount} detail="Ready to take on tasks" icon="↗" tone="green" />
            <MetricCard label="Model providers" value={loading ? '—' : providers.length} detail="Available integrations" icon="◇" tone="blue" />
            <MetricCard label="Conversations" value={loading ? '—' : conversations.length} detail="Across all agents" icon="◷" tone="orange" />
          </section>

          <section className="content-grid">
            <div className="panel agents-panel">
              <div className="panel-heading">
                <div>
                  <h2>Specialized agents</h2>
                  <p>Your team of AI specialists</p>
                </div>
                <button className="text-button" onClick={() => setActiveSection('Agents')} type="button">View all <span aria-hidden="true">→</span></button>
              </div>

              {loading ? (
                <div className="loading-state"><span className="spinner" /> Loading agents…</div>
              ) : agents.length === 0 ? (
                <div className="empty-state">
                  <span className="empty-icon">✳</span>
                  <strong>No agents yet</strong>
                  <span>Create an agent to start building your AI team.</span>
                  <button className="secondary-button" onClick={() => setIsCreateOpen(true)} type="button">Create your first agent</button>
                </div>
              ) : (
                <div className="agent-list">
                  {agents.map((agent) => (
                    <AgentRow agent={agent} key={agent.id} />
                  ))}
                </div>
              )}
            </div>

            <div className="right-column">
              <div className="panel providers-panel">
                <div className="panel-heading">
                  <div>
                    <h2>Model providers</h2>
                    <p>Connected to your agents</p>
                  </div>
                  <button className="more-button" aria-label="More provider options" type="button">···</button>
                </div>
                {loading ? (
                  <div className="loading-state compact"><span className="spinner" /> Loading…</div>
                ) : providers.length === 0 ? (
                  <p className="subtle-empty">No model providers are configured.</p>
                ) : (
                  <div className="provider-list">
                    {providers.map((provider) => {
                      const count = agents.filter((agent) => agent.modelProvider === provider.name).length
                      return (
                        <div className="provider-row" key={provider.name}>
                          <ProviderMark name={provider.name} />
                          <div className="provider-copy"><strong>{provider.displayName}</strong><span>{provider.requiresApiKey ? 'API key required' : 'Local connection'}</span></div>
                          <span className="provider-agent-count">{count} {count === 1 ? 'agent' : 'agents'}</span>
                        </div>
                      )
                    })}
                  </div>
                )}
              </div>

              <div className="insight-card">
                <div className="insight-icon">✦</div>
                <p className="insight-label">GETTING STARTED</p>
                <h3>Build your AI team</h3>
                <p className="insight-copy">Each agent has a focused role. Add specialists to help your workspace take on more complex tasks.</p>
                <button onClick={() => setIsCreateOpen(true)} type="button">Create an agent <span aria-hidden="true">→</span></button>
                <div className="insight-decoration" aria-hidden="true">✳</div>
              </div>
            </div>
          </section>
            </>
          )}

          <footer className="page-footer">
            <span>AgentForge <span className="footer-dot">·</span> Multi-agent workspace</span>
            <span><span className={`status-dot ${apiOnline ? 'online' : ''}`} /> {apiOnline ? 'All systems operational' : 'Waiting for API connection'}</span>
          </footer>
        </div>
      </main>

      {isCreateOpen && (
        <div className="modal-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget) setIsCreateOpen(false)
        }}>
          <section className="modal" aria-labelledby="create-agent-title" aria-modal="true" role="dialog">
            <div className="modal-heading">
              <div><p className="eyebrow">WORKSPACE CONFIGURATION</p><h2 id="create-agent-title">Create an agent</h2></div>
              <button className="modal-close" aria-label="Close dialog" onClick={() => setIsCreateOpen(false)} type="button">×</button>
            </div>
            <p className="modal-description">Give your agent a clear purpose and choose its specialization.</p>
            <form className="agent-form" onSubmit={(event) => void handleCreateAgent(event)}>
              <label>Agent name<input autoFocus maxLength={100} name="name" placeholder="e.g. Research Assistant" required /></label>
              <label>Description<textarea maxLength={500} name="description" placeholder="What should this agent help with?" required rows={3} /></label>
              <div className="form-row">
                <label>Specialization
                  <select defaultValue="Knowledge" name="role">
                    {Object.entries(roleNames).map(([role, label]) => <option key={role} value={role}>{label}</option>)}
                  </select>
                </label>
                <label>Model provider
                  <select defaultValue={providers[0]?.name ?? 'Gemini'} name="provider">
                    {providers.map((provider) => <option key={provider.name} value={provider.name}>{provider.displayName}</option>)}
                  </select>
                </label>
              </div>
              <div className="modal-actions">
                <button className="secondary-button" onClick={() => setIsCreateOpen(false)} type="button">Cancel</button>
                <button className="primary-button" disabled={isSaving || providers.length === 0} type="submit">
                  {isSaving ? 'Creating…' : 'Create agent'}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  )
}

function MetricCard({ label, value, detail, icon, tone }: { label: string; value: string | number; detail: string; icon: string; tone: string }) {
  return (
    <article className="metric-card">
      <div className={`metric-icon ${tone}`} aria-hidden="true">{icon}</div>
      <div className="metric-value">{value}</div>
      <div className="metric-label">{label}</div>
      <div className="metric-detail">{detail}</div>
    </article>
  )
}

function AgentRow({ agent }: { agent: Agent }) {
  const role: AgentRole = agent.role in roleNames ? agent.role : 'Knowledge'
  return (
    <article className="agent-row">
      <span className={`agent-avatar ${roleStyles[role]}`} aria-hidden="true">{roleNames[role].slice(0, 1)}</span>
      <div className="agent-info">
        <div className="agent-title-line"><strong>{agent.name}</strong><span className={`agent-status ${agent.isEnabled ? 'enabled' : ''}`}><span className="status-dot" />{agent.isEnabled ? 'Active' : 'Disabled'}</span></div>
        <p>{agent.description}</p>
      </div>
      <div className="agent-meta"><span className={`role-tag ${roleStyles[role]}`}>{roleNames[role]}</span><span className="model-tag">{agent.modelProvider}</span></div>
      <button className="row-more" aria-label={`More options for ${agent.name}`} type="button">···</button>
    </article>
  )
}

function ProviderMark({ name }: { name: string }) {
  const mark = name === 'Gemini' ? '✦' : name === 'OpenAI' ? '◎' : '◉'
  const tone = name === 'Gemini' ? 'gemini' : name === 'OpenAI' ? 'openai' : 'ollama'
  return <span className={`provider-mark ${tone}`} aria-hidden="true">{mark}</span>
}

export default App
