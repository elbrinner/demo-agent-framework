import React from 'react';

interface StructuredViewerProps {
  personInfo?: any;
  raw?: string;
  schema?: any;
}

// Validación mínima contra schema (solo required + tipos primarios)
function validate(person: any, schema: any): { ok: boolean; issues: string[] } {
  if (!schema || typeof schema !== 'object') return { ok: true, issues: [] };
  const issues: string[] = [];
  const required = schema.required || [];
  for (const r of required) {
    if (person == null || person[r] == null || person[r] === '') issues.push(`Campo requerido faltante: ${r}`);
  }
  // Tipos primarios simples
  const props = schema.properties || {};
  for (const key of Object.keys(person || {})) {
    const def = props[key];
    if (!def) continue;
    const type = def.type;
    const value = person[key];
    if (type === 'string' && value != null && typeof value !== 'string') issues.push(`Campo ${key} debería ser string`);
    if (type === 'number' && value != null && typeof value !== 'number') issues.push(`Campo ${key} debería ser number`);
    if (type === 'array' && value != null && !Array.isArray(value)) issues.push(`Campo ${key} debería ser array`);
  }
  return { ok: issues.length === 0, issues };
}

export const StructuredViewer: React.FC<StructuredViewerProps> = ({ personInfo, raw, schema }) => {
  const validation = validate(personInfo, schema);
  return (
    <div className="border border-indigo-300 rounded-lg p-4 bg-indigo-50">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-semibold text-indigo-900 m-0">Perfil estructurado</h3>
        <span className={`px-2 py-1 rounded text-xs ${validation.ok ? 'bg-emerald-200 text-emerald-900' : 'bg-red-200 text-red-900'}`}>{validation.ok ? 'válido' : 'con issues'}</span>
      </div>
      {personInfo ? (
        <div className="grid gap-2 text-sm">
          {personInfo.name && <div><span className="font-medium">Nombre:</span> {personInfo.name}</div>}
          {personInfo.age != null && <div><span className="font-medium">Edad:</span> {personInfo.age}</div>}
          {personInfo.occupation && <div><span className="font-medium">Ocupación:</span> {personInfo.occupation}</div>}
          {Array.isArray(personInfo.skills) && personInfo.skills.length > 0 && (
            <div>
              <span className="font-medium">Habilidades:</span>
              <ul className="list-disc list-inside m-0 pl-4">
                {personInfo.skills.map((s: string, i: number) => <li key={i}>{s}</li>)}
              </ul>
            </div>
          )}
          {personInfo.summary && <div><span className="font-medium">Resumen:</span> {personInfo.summary}</div>}
        </div>
      ) : (
        <div className="text-xs italic text-gray-600">Sin datos estructurados parseados.</div>
      )}
      {!validation.ok && (
        <div className="mt-3 p-2 bg-red-50 border border-red-200 rounded">
          <div className="text-xs font-semibold text-red-800 mb-1">Problemas de validación</div>
          <ul className="m-0 pl-4 list-disc text-[11px] text-red-800">
            {validation.issues.map((iss, i) => <li key={i}>{iss}</li>)}
          </ul>
        </div>
      )}
      <details className="mt-3">
        <summary className="cursor-pointer text-xs text-gray-600 hover:text-gray-800">Ver JSON bruto</summary>
        <pre className="mt-2 whitespace-pre-wrap font-mono text-[11px] text-gray-700">{raw || '(vacío)'}</pre>
      </details>
      {schema && (
        <details className="mt-2">
          <summary className="cursor-pointer text-xs text-gray-600 hover:text-gray-800">Ver Schema</summary>
          <pre className="mt-2 whitespace-pre-wrap font-mono text-[11px] text-gray-700">{JSON.stringify(schema, null, 2)}</pre>
        </details>
      )}
    </div>
  );
};

export default StructuredViewer;
