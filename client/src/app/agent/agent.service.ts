import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import {
  IAgentInfo, IAgentResult, IAgentRun, IAgentStats, IEvalRequest, IEvalRun, IEvalRunDetail, IPage
} from './agent.models';

/**
 * Talks to the agent API (api/agent/*).
 * The JWT interceptor adds the login token automatically, so a signed-in user
 * gets the order tools without anything extra here.
 */
@Injectable({
  providedIn: 'root'
})
export class AgentService {
  private baseUrl = environment.apiUrl + 'agent/';

  constructor(private http: HttpClient) { }

  /** Sends one message. Pass the sessionId from the previous answer to continue the conversation. */
  chat(message: string, sessionId: string | null): Observable<IAgentResult> {
    return this.http.post<IAgentResult>(this.baseUrl + 'chat', { message, sessionId });
  }

  getInfo(): Observable<IAgentInfo> {
    return this.http.get<IAgentInfo>(this.baseUrl + 'info');
  }

  getRuns(page: number, pageSize: number): Observable<IPage<IAgentRun>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<IPage<IAgentRun>>(this.baseUrl + 'runs', { params });
  }

  getRun(id: string): Observable<IAgentRun> {
    return this.http.get<IAgentRun>(this.baseUrl + 'runs/' + id);
  }

  getStats(): Observable<IAgentStats> {
    return this.http.get<IAgentStats>(this.baseUrl + 'stats');
  }

  /** Starts an eval run in the background. The server answers at once with status "Running". */
  startEval(request: IEvalRequest): Observable<IEvalRun> {
    return this.http.post<IEvalRun>(this.baseUrl + 'evals', request);
  }

  getEvals(page: number, pageSize: number): Observable<IPage<IEvalRun>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<IPage<IEvalRun>>(this.baseUrl + 'evals', { params });
  }

  getEval(id: string): Observable<IEvalRunDetail> {
    return this.http.get<IEvalRunDetail>(this.baseUrl + 'evals/' + id);
  }
}
