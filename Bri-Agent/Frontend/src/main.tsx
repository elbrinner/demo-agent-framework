import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { DemoList } from './modules/DemoList';
import { DemoHost } from './modules/DemoHost';
import { Workflows } from './modules/Workflows';
import { JokesFactory } from './modules/JokesFactory';
import './index.css';

const Root: React.FC = () => (
	<BrowserRouter>
		<div className="px-4 py-2 border-b border-gray-200 flex gap-4 bg-white shadow-sm">
			<Link to="/" className="text-blue-600 hover:text-blue-800 font-medium transition-colors">Demos</Link>
			<Link to="/workflows" className="text-blue-600 hover:text-blue-800 font-medium transition-colors">Workflows</Link>
			<Link to="/jokes" className="text-blue-600 hover:text-blue-800 font-medium transition-colors">Fábrica de Chistes</Link>
		</div>
		<Routes>
			<Route path="/" element={<DemoList />} />
			<Route path="/demos/:id" element={<DemoHost />} />
			<Route path="/workflows" element={<Workflows />} />
			<Route path="/jokes" element={<JokesFactory />} />
		</Routes>
	</BrowserRouter>
);

createRoot(document.getElementById('root')!).render(<Root />);
