export type AgentRole = 'Knowledge' | 'Developer' | 'Analytics' | 'Validation'

export interface Agent {
  id: string
  name: string
  description: string
  role: AgentRole
  modelProvider: string
  isEnabled: boolean
  createdAtUtc: string
}

export interface CreateAgentRequest {
  name: string
  description: string
  role: AgentRole
  modelProvider: string
  isEnabled: boolean
}

export interface ModelProvider {
  name: string
  displayName: string
  description: string
  requiresApiKey: boolean
}

export interface Conversation {
  id: string
  agentId: string | null
  title: string
  messages: Array<{
    id: string
    role: string
    content: string
    sources?: string[]
    createdAtUtc: string
  }>
  updatedAtUtc: string
}

export interface ChatResponse {
  conversationId: string
  agentId: string
  agentName: string
  assistantMessage: string
  sources: string[]
  createdAtUtc: string
}

export interface ChatRequest {
  conversationId?: string
  agentId?: string
  message: string
}

export interface KnowledgeSearchResult {
  source: string
  content: string
}

export interface KnowledgeDocumentResponse {
  id: string
  name: string
  chunkCount: number
  createdAtUtc: string
}
