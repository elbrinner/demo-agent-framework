import React from 'react';
import StructuredViewer from './StructuredViewer';
import { Link } from 'react-router-dom';

interface ResponsePanelProps { response: any; streamText?: string; }

export const ResponsePanel: React.FC<ResponsePanelProps> = ({ response, streamText }) => {
  const hasStream = !!streamText;
  const meta = response?.meta;
  const ui = meta?.ui;
  return (
    <div className="border border-gray-300 rounded-lg p-4 min-h-32 bg-gray-50">
      <div className="flex items-center justify-between mb-3">
        <div className="font-semibold text-gray-900">Respuesta</div>
        {ui && (
          <div className="flex flex-wrap gap-2 items-center text-xs">
            <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded-md font-medium">modo: {ui.mode}</span>
            {ui.recommendedView && (
              <span className="px-2 py-1 bg-indigo-100 text-indigo-800 rounded-md">view: {ui.recommendedView}</span>
            )}
            {Array.isArray(ui.capabilities) && ui.capabilities.map((c:string) => (
              <span key={c} className="px-2 py-1 bg-emerald-100 text-emerald-800 rounded-md">{c}</span>
            ))}
          </div>
        )}
      </div>
      {response && (
        <>
          {ui?.mode === 'single' && response.response && (
            <div className="mb-2 p-3 bg-white border border-gray-200 rounded shadow-sm text-sm text-gray-800 whitespace-pre-wrap">{response.response}</div>
          )}
          {ui?.mode === 'workflow' && (
            <div className="mb-2 p-3 bg-yellow-50 border-l-4 border-yellow-400 rounded shadow-sm text-sm text-gray-800">
              <div className="font-semibold mb-1">Workflow detectado</div>
              <p className="m-0 mb-2 text-gray-700">Esta respuesta anuncia un flujo de trabajo (meta.ui.mode = workflow). Usa la vista dedicada para visualizar pasos y eventos en tiempo real.</p>
              <Link to="/workflows" className="inline-block px-3 py-1 bg-yellow-500 hover:bg-yellow-600 text-white rounded text-xs">Abrir vista Workflows</Link>
            </div>
          )}
          {ui?.mode === 'structured' && (
            <div className="mb-3">
              <StructuredViewer
                personInfo={response.personInfo}
                raw={JSON.stringify(response.personInfo, null, 2)}
                schema={response.schema}
              />
            </div>
          )}
          {ui?.mode === 'tools' && (
            <div className="mb-3 p-3 bg-white border border-gray-200 rounded">
              <div className="text-sm font-semibold mb-2">Invocaciones de tools</div>
              {Array.isArray(response.toolsUsed) && response.toolsUsed.length > 0 ? (
                <ul className="m-0 pl-4 list-disc text-sm text-gray-800">
                  {response.toolsUsed.map((t:any, i:number) => (
                    <li key={i}><span className="font-medium">{t.tool}</span>{t.args ? `(${Object.entries(t.args).map(([k,v])=>`${k}:${v}`).join(', ')})` : ''}: {t.output}</li>
                  ))}
                </ul>
              ) : (
                <div className="text-xs text-gray-600">No se invocaron tools.</div>
              )}
              {response.answer && (
                <div className="mt-3 p-2 bg-gray-50 border border-dashed border-gray-300 rounded text-xs whitespace-pre-wrap">
                  {response.answer}
                </div>
              )}
              {Array.isArray(response.availableTools) && response.availableTools.length > 0 && (
                <details className="mt-2">
                  <summary className="cursor-pointer text-xs text-gray-600 hover:text-gray-800">Ver catálogo de tools</summary>
                  <ul className="m-0 pl-4 list-disc text-[12px] text-gray-700">
                    {response.availableTools.map((t:any, i:number) => (
                      <li key={i}><span className="font-medium">{t.name}</span>: {t.description}</li>
                    ))}
                  </ul>
                </details>
              )}
            </div>
          )}
          {/* Fallback JSON completo para depuración */}
          <details className="mt-2">
            <summary className="cursor-pointer text-xs text-gray-600 hover:text-gray-800">Ver JSON completo</summary>
            <pre className="mt-2 whitespace-pre-wrap font-mono text-[11px] text-gray-700">{JSON.stringify(response, null, 2)}</pre>
          </details>
        </>
      )}
      {!response && hasStream && (
        <pre className="m-0 whitespace-pre-wrap font-mono text-sm text-gray-700">{streamText}</pre>
      )}
      {!response && !hasStream && (
        <pre className="m-0 whitespace-pre-wrap font-mono text-sm text-gray-700"><span className="italic text-gray-500">Sin respuesta aún…</span></pre>
      )}
    </div>
  );
};
