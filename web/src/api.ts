import type {
  Agent, ChatRequest, ChatResponse, Conversation, CreateAgentRequest,
  KnowledgeDocumentResponse, KnowledgeSearchResult, ModelProvider,
} from './types'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/$/, '')
let accessToken: string | undefined

export function setAccessToken(token: string | undefined) {
  accessToken = token
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const body = await response.text()
    let detail = body
    try {
      const problem = JSON.parse(body) as { detail?: string; title?: string }
      detail = problem.detail ?? problem.title ?? body
    } catch {
      detail = body
    }
    throw new Error(detail || `Request failed with status ${response.status}.`)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function getAgents(): Promise<Agent[]> {
  return request('/agents')
}

export function getProviders(): Promise<ModelProvider[]> {
  return request('/providers')
}

export function getConversations(): Promise<Conversation[]> {
  return request('/conversations')
}

export function getHealth(): Promise<{ status: string; service: string }> {
  return request('/health')
}

export function createAgent(agent: CreateAgentRequest): Promise<Agent> {
  return request('/agents', {
    method: 'POST',
    body: JSON.stringify(agent),
  })
}

export function sendChat(message: ChatRequest): Promise<ChatResponse> {
  return request('/chat', { method: 'POST', body: JSON.stringify(message) })
}

export function getConversation(id: string): Promise<Conversation> {
  return request(`/conversations/${encodeURIComponent(id)}`)
}

export function ingestKnowledgeDocument(name: string, content: string): Promise<KnowledgeDocumentResponse> {
  return request('/knowledge/documents', { method: 'POST', body: JSON.stringify({ name, content }) })
}

export function searchKnowledge(query: string): Promise<KnowledgeSearchResult[]> {
  return request(`/knowledge/search?query=${encodeURIComponent(query)}`)
}
