import "./App.css";

const setupItems = [
  {
    label: "Backend",
    value: "ASP.NET Core Web API"
  },
  {
    label: "Frontend",
    value: "React + TypeScript"
  },
  {
    label: "Import Folders",
    value: "Incoming / Processed / Error"
  }
];

function App() {
  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Issue #1</p>
          <h1>Smart File Import Service</h1>
        </div>
        <nav aria-label="Main navigation">
          <a href="#dashboard">Dashboard</a>
          <a href="#upload">Upload</a>
          <a href="#history">History</a>
        </nav>
      </header>

      <section className="workspace" aria-labelledby="setup-title">
        <div>
          <p className="eyebrow">Project setup</p>
          <h2 id="setup-title">Ready for the next issue</h2>
          <p className="summary">
            The backend, frontend, and file processing folders are now in place.
          </p>
        </div>

        <div className="status-grid" aria-label="Setup status">
          {setupItems.map((item) => (
            <article className="status-card" key={item.label}>
              <span>{item.label}</span>
              <strong>{item.value}</strong>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}

export default App;
