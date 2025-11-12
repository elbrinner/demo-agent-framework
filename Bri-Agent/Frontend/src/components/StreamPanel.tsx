import React, { useMemo } from 'react';

export const StreamPanel: React.FC<{ chunks: string[] }>= ({ chunks }) => {
  const text = useMemo(() => chunks.join(''), [chunks]);
  return (
    <div className="border border-gray-300 rounded-lg p-4 min-h-32 bg-blue-50">
      <div className="font-semibold mb-3 text-gray-900">EventStream (tokens)</div>
      {!chunks.length && (
        <div className="italic text-gray-500">Esperando tokens…</div>
      )}
      {!!chunks.length && (
        <>
          <div className="mb-3 whitespace-pre-wrap font-mono text-sm text-gray-700 bg-white/60 p-2 rounded border border-blue-200">
            {text}
          </div>
          <div className="text-xs text-gray-600 mb-2">{chunks.length} tokens • {text.length} chars acumulados</div>
          <ol className="m-0 pl-5 space-y-1">
            {chunks.map((c,i) => (
              <li key={i} className="font-mono text-xs text-gray-800">
                <span className="px-1 py-0.5 bg-blue-100 text-blue-800 rounded mr-2">#{i+1}</span>
                <span>{c}</span>
                <span className="ml-2 text-gray-500">({c.length})</span>
              </li>
            ))}
          </ol>
        </>
      )}
    </div>
  );
};
