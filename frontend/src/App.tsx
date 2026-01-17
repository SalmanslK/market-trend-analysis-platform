import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import Dashboard from './pages/Dashboard'
import InstrumentView from './pages/InstrumentView'
import Comparison from './pages/Comparison'
import Settings from './pages/Settings'
import Navigation from './components/layout/Navigation'

function App() {
  return (
    <Router>
      <div className="app">
        <Navigation />
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/instrument/:symbol" element={<InstrumentView />} />
          <Route path="/comparison" element={<Comparison />} />
          <Route path="/settings" element={<Settings />} />
        </Routes>
      </div>
    </Router>
  )
}

export default App