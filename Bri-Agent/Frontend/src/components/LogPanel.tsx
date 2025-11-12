import React from 'react';

export const LogPanel: React.FC<{ logs: string[] }> = ({ logs }) => {
  return (
    <div className="border border-gray-300 rounded-lg p-4 h-64 overflow-auto bg-gray-50">
      <div className="font-semibold mb-3 text-gray-900">Logs</div>
      {logs.length === 0 ? (
        <div className="text-gray-500 italic">Sin logs aún…</div>
      ) : (
        <ul className="m-0 pl-0 space-y-3">
          {logs.map((l, i) => (
            <li key={i} className="font-mono text-xs text-gray-700 whitespace-pre-wrap break-words">{l}</li>
          ))}
        </ul>
      )}
    </div>
  );
};
