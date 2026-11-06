import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import Dashboard from './pages/Dashboard'
import InstrumentView from './pages/InstrumentView'
import Comparison from './pages/Comparison'
import News from './pages/News'
import Guide from './pages/Guide'
import Settings from './pages/Settings'
import Navigation from './components/layout/Navigation'

function App() {
  return (
    <Router future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <div className="app">
        <Navigation />
        {/* Same width as the navigation bar so page content lines up with it */}
        <main style={{ maxWidth: '1200px', margin: '0 auto' }}>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/instrument/:symbol" element={<InstrumentView />} />
            <Route path="/news" element={<News />} />
            <Route path="/comparison" element={<Comparison />} />
            <Route path="/guide" element={<Guide />} />
            <Route path="/settings" element={<Settings />} />
          </Routes>
        </main>
      </div>
    </Router>
  )
}

export default App
