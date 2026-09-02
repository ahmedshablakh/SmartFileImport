import { ChangeEvent, FormEvent, useEffect, useMemo, useRef, useState } from "react";
import "./App.css";

type UploadStatus =
  | {
      kind: "idle";
    }
  | {
      kind: "success";
      fileName: string;
      message: string;
    }
  | {
      kind: "error";
      message: string;
    };

type UploadApiResponse = {
  fileName?: string;
  message?: string;
  error?: string;
  rawText?: string;
};

type ImportHistoryItem = {
  id: number;
  fileName: string;
  status: string;
  recordCount: number;
  processedAt: string;
  errorMessage?: string | null;
};

type DashboardStatistics = {
  totalFiles: number;
  successfulFiles: number;
  failedFiles: number;
  totalImportedRecords: number;
};

type Theme = "light" | "dark";

const supportedExtensions = [".csv", ".xlsx"];
const requiredColumns = ["Name", "Email", "Department", "Salary"];
const fileRequirementRules = [
  "First row must contain the required column names.",
  "Email values must use a valid email format.",
  "Salary must be a number greater than zero."
];
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5107").replace(
  /\/$/,
  ""
);
const emptyDashboardStatistics: DashboardStatistics = {
  totalFiles: 0,
  successfulFiles: 0,
  failedFiles: 0,
  totalImportedRecords: 0
};
const themeStorageKey = "smart-file-import-theme";
const autoRefreshIntervalMs = 2000;
const autoRefreshMaxAttempts = 30;

function getInitialTheme(): Theme {
  const storedTheme = window.localStorage.getItem(themeStorageKey);

  if (storedTheme === "light" || storedTheme === "dark") {
    return storedTheme;
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function getFileExtension(fileName: string) {
  const extensionStart = fileName.lastIndexOf(".");

  return extensionStart === -1 ? "" : fileName.slice(extensionStart).toLowerCase();
}

function isSupportedFile(file: File) {
  return supportedExtensions.includes(getFileExtension(file.name));
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kilobytes = bytes / 1024;

  if (kilobytes < 1024) {
    return `${kilobytes.toFixed(1)} KB`;
  }

  return `${(kilobytes / 1024).toFixed(1)} MB`;
}

async function readUploadResponse(response: Response): Promise<UploadApiResponse> {
  const contentType = response.headers.get("content-type") ?? "";
  const responseText = await response.text();

  if (contentType.includes("application/json") && responseText) {
    try {
      return JSON.parse(responseText) as UploadApiResponse;
    } catch {
      return {
        rawText: responseText
      };
    }
  }

  return {
    rawText: responseText || response.statusText
  };
}

function getApiErrorMessage(
  response: Response,
  responseBody: UploadApiResponse,
  fallbackMessage: string
) {
  if (responseBody.error?.trim()) {
    return responseBody.error;
  }

  if (responseBody.message?.trim()) {
    return responseBody.message;
  }

  if (response.status >= 500) {
    return `${fallbackMessage} The backend returned ${response.status}. Check the database connection and server logs.`;
  }

  const rawText = responseBody.rawText?.trim();

  if (!rawText) {
    return fallbackMessage;
  }

  return rawText.length > 180 ? `${rawText.slice(0, 180)}...` : rawText;
}

async function readApiError(response: Response, fallbackMessage: string) {
  const responseBody = await readUploadResponse(response);

  return getApiErrorMessage(response, responseBody, fallbackMessage);
}

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === "AbortError";
}

function formatRecordCount(recordCount: number) {
  return new Intl.NumberFormat().format(recordCount);
}

function formatProcessedAt(processedAt: string) {
  const date = new Date(processedAt);

  if (Number.isNaN(date.getTime())) {
    return processedAt;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(date);
}

function getStatusClassName(status: string) {
  return status.toLowerCase() === "success"
    ? "status-pill status-pill--success"
    : "status-pill status-pill--failed";
}

function wasProcessedAfter(processedAt: string, earliestDate: Date) {
  const processedDate = new Date(processedAt);
  const clockDriftToleranceMs = 5000;

  return (
    !Number.isNaN(processedDate.getTime()) &&
    processedDate.getTime() >= earliestDate.getTime() - clockDriftToleranceMs
  );
}

function App() {
  const [theme, setTheme] = useState<Theme>(() => getInitialTheme());
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [status, setStatus] = useState<UploadStatus>({ kind: "idle" });
  const [isUploading, setIsUploading] = useState(false);
  const [dashboardStatistics, setDashboardStatistics] = useState<DashboardStatistics>(
    emptyDashboardStatistics
  );
  const [isDashboardLoading, setIsDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState("");
  const [importHistory, setImportHistory] = useState<ImportHistoryItem[]>([]);
  const [selectedImportId, setSelectedImportId] = useState<number | null>(null);
  const [selectedImport, setSelectedImport] = useState<ImportHistoryItem | null>(null);
  const [isHistoryLoading, setIsHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState("");
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [lastRefreshAt, setLastRefreshAt] = useState<Date | null>(null);
  const [autoRefreshMessage, setAutoRefreshMessage] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);
  const autoRefreshTimersRef = useRef<number[]>([]);

  const selectedFileExtension = useMemo(
    () => (selectedFile ? getFileExtension(selectedFile.name).replace(".", "") : ""),
    [selectedFile]
  );

  const canUpload = selectedFile !== null && isSupportedFile(selectedFile) && !isUploading;
  const themeButtonLabel = theme === "dark" ? "Light mode" : "Dark mode";

  function clearAutoRefreshTimers() {
    autoRefreshTimersRef.current.forEach((timerId) => window.clearTimeout(timerId));
    autoRefreshTimersRef.current = [];
  }

  async function loadDashboardStatistics(signal?: AbortSignal, showLoading = true) {
    if (showLoading) {
      setIsDashboardLoading(true);
    }

    setDashboardError("");

    try {
      const response = await fetch(`${apiBaseUrl}/api/dashboard`, { signal });

      if (!response.ok) {
        throw new Error(await readApiError(response, "Dashboard statistics could not be loaded."));
      }

      const statistics = (await response.json()) as DashboardStatistics;

      setDashboardStatistics(statistics);
      setLastRefreshAt(new Date());

      return statistics;
    } catch (error) {
      if (isAbortError(error)) {
        return emptyDashboardStatistics;
      }

      setDashboardError(
        error instanceof Error ? error.message : "Dashboard statistics could not be loaded."
      );

      if (showLoading) {
        setDashboardStatistics(emptyDashboardStatistics);
      }

      return emptyDashboardStatistics;
    } finally {
      if (showLoading && !signal?.aborted) {
        setIsDashboardLoading(false);
      }
    }
  }

  async function loadImportHistory(signal?: AbortSignal, showLoading = true) {
    if (showLoading) {
      setIsHistoryLoading(true);
    }

    setHistoryError("");

    try {
      const response = await fetch(`${apiBaseUrl}/api/imports`, { signal });

      if (!response.ok) {
        throw new Error(await readApiError(response, "Import history could not be loaded."));
      }

      const imports = (await response.json()) as ImportHistoryItem[];

      setImportHistory(imports);
      setLastRefreshAt(new Date());
      setSelectedImportId((currentId) => {
        if (currentId !== null && imports.some((importRecord) => importRecord.id === currentId)) {
          return currentId;
        }

        return imports[0]?.id ?? null;
      });

      if (imports.length === 0) {
        setSelectedImport(null);
      }

      return imports;
    } catch (error) {
      if (isAbortError(error)) {
        return [];
      }

      if (showLoading) {
        setImportHistory([]);
        setSelectedImportId(null);
        setSelectedImport(null);
      }

      setHistoryError(error instanceof Error ? error.message : "Import history could not be loaded.");

      return [];
    } finally {
      if (showLoading && !signal?.aborted) {
        setIsHistoryLoading(false);
      }
    }
  }

  async function refreshDashboardAndHistory(signal?: AbortSignal, showLoading = true) {
    const [imports] = await Promise.all([
      loadImportHistory(signal, showLoading),
      loadDashboardStatistics(signal, showLoading)
    ]);

    return imports;
  }

  function refreshAfterUpload(fileName: string, queuedAt: Date) {
    clearAutoRefreshTimers();
    setAutoRefreshMessage(`Auto refreshing ${fileName}...`);

    async function runRefreshAttempt(attempt: number) {
      const imports = await refreshDashboardAndHistory(undefined, false);
      const processedImport = imports.find(
        (importRecord) =>
          importRecord.fileName === fileName && wasProcessedAfter(importRecord.processedAt, queuedAt)
      );

      if (processedImport) {
        setSelectedImportId(processedImport.id);
        setAutoRefreshMessage(`Auto updated: ${processedImport.status}.`);

        const timerId = window.setTimeout(() => setAutoRefreshMessage(""), 5000);
        autoRefreshTimersRef.current.push(timerId);
        return;
      }

      if (attempt < autoRefreshMaxAttempts) {
        const timerId = window.setTimeout(() => {
          void runRefreshAttempt(attempt + 1);
        }, autoRefreshIntervalMs);
        autoRefreshTimersRef.current.push(timerId);
        return;
      }

      setAutoRefreshMessage("Still processing. Refreshing paused.");
    }

    void runRefreshAttempt(1);
  }

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
    window.localStorage.setItem(themeStorageKey, theme);
  }, [theme]);

  useEffect(() => {
    const abortController = new AbortController();

    void refreshDashboardAndHistory(abortController.signal);

    return () => abortController.abort();
  }, []);

  useEffect(() => clearAutoRefreshTimers, []);

  useEffect(() => {
    if (selectedImportId === null) {
      setSelectedImport(null);
      setDetailError("");
      return;
    }

    const abortController = new AbortController();

    async function loadImportDetails() {
      setIsDetailLoading(true);
      setDetailError("");

      try {
        const response = await fetch(`${apiBaseUrl}/api/imports/${selectedImportId}`, {
          signal: abortController.signal
        });

        if (!response.ok) {
          throw new Error(await readApiError(response, "Import details could not be loaded."));
        }

        setSelectedImport((await response.json()) as ImportHistoryItem);
      } catch (error) {
        if (isAbortError(error)) {
          return;
        }

        setSelectedImport(null);
        setDetailError(error instanceof Error ? error.message : "Import details could not be loaded.");
      } finally {
        if (!abortController.signal.aborted) {
          setIsDetailLoading(false);
        }
      }
    }

    void loadImportDetails();

    return () => abortController.abort();
  }, [selectedImportId]);

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null;

    setSelectedFile(file);

    if (file === null) {
      setStatus({ kind: "idle" });
      return;
    }

    if (!isSupportedFile(file)) {
      setStatus({
        kind: "error",
        message: "Only CSV and XLSX files can be uploaded."
      });
      return;
    }

    setStatus({ kind: "idle" });
  }

  async function handleUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (selectedFile === null) {
      setStatus({
        kind: "error",
        message: "Select a CSV or XLSX file before uploading."
      });
      return;
    }

    if (!isSupportedFile(selectedFile)) {
      setStatus({
        kind: "error",
        message: "Only CSV and XLSX files can be uploaded."
      });
      return;
    }

    const formData = new FormData();
    formData.append("file", selectedFile);

    const queuedAt = new Date();

    setIsUploading(true);
    setStatus({ kind: "idle" });

    try {
      const response = await fetch(`${apiBaseUrl}/api/files/upload`, {
        method: "POST",
        body: formData
      });
      const responseBody = await readUploadResponse(response);

      if (!response.ok) {
        setStatus({
          kind: "error",
          message: getApiErrorMessage(response, responseBody, "Upload failed.")
        });
        return;
      }

      const queuedFileName = responseBody.fileName ?? selectedFile.name;

      setStatus({
        kind: "success",
        fileName: queuedFileName,
        message:
          responseBody.message ??
          "File uploaded successfully. History and dashboard will update automatically."
      });
      refreshAfterUpload(queuedFileName, queuedAt);
      setSelectedFile(null);

      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    } catch {
      setStatus({
        kind: "error",
        message: `Could not reach the upload API at ${apiBaseUrl}.`
      });
    } finally {
      setIsUploading(false);
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Smart imports</p>
          <h1>Smart File Import</h1>
        </div>
        <div className="topbar__actions">
          <nav aria-label="Main navigation">
            <a href="#upload">Upload</a>
            <a href="#dashboard">Dashboard</a>
            <a href="#history">History</a>
          </nav>
          <button
            className="theme-toggle"
            type="button"
            aria-pressed={theme === "dark"}
            onClick={() => setTheme((currentTheme) => (currentTheme === "dark" ? "light" : "dark"))}
          >
            <span>{themeButtonLabel}</span>
            <span className="theme-toggle__switch" aria-hidden="true">
              <span />
            </span>
          </button>
        </div>
      </header>

      <section className="upload-workspace" id="upload" aria-labelledby="upload-title">
        <div className="upload-panel">
          <div className="panel-heading">
            <p className="eyebrow">File intake</p>
            <h2 id="upload-title">Upload employee data</h2>
            <p className="panel-copy">Prepare the first row exactly as shown before uploading.</p>
          </div>

          <form className="upload-form" onSubmit={handleUpload}>
            <input
              ref={fileInputRef}
              id="employee-file"
              className="file-input"
              type="file"
              accept=".csv,.xlsx"
              onChange={handleFileChange}
            />
            <div className="upload-row">
              <label
                className={`file-target ${selectedFile ? "file-target--selected" : ""}`}
                htmlFor="employee-file"
              >
                <span className="file-target__badge" aria-hidden="true">
                  {selectedFileExtension ? selectedFileExtension.toUpperCase() : "FILE"}
                </span>
                <span className="file-target__content">
                  <span className="file-target__name">
                    {selectedFile ? selectedFile.name : "Select CSV or XLSX file"}
                  </span>
                  <span className="file-target__meta">
                    {selectedFile
                      ? `${formatFileSize(selectedFile.size)} - ${selectedFileExtension.toUpperCase()}`
                      : "CSV and XLSX files are accepted"}
                  </span>
                </span>
              </label>

              <div className="upload-actions">
                <button className="primary-button" type="submit" disabled={!canUpload}>
                  {isUploading ? "Uploading..." : "Upload file"}
                </button>
              </div>
            </div>

            <div className="file-requirements" aria-label="Required file contents">
              <div className="requirements-header">
                <span className="requirements-title">Required file structure</span>
                <div className="required-columns">
                  {requiredColumns.map((column) => (
                    <code key={column}>{column}</code>
                  ))}
                </div>
              </div>

              <ul className="requirements-list">
                {fileRequirementRules.map((rule) => (
                  <li key={rule}>{rule}</li>
                ))}
              </ul>
            </div>
          </form>

          {status.kind !== "idle" ? (
            <div
              className={`notice notice--${status.kind}`}
              role={status.kind === "error" ? "alert" : "status"}
            >
              <strong>{status.kind === "success" ? status.fileName : "Upload error"}</strong>
              <span>{status.message}</span>
            </div>
          ) : null}
        </div>
      </section>

      <section className="dashboard-workspace" id="dashboard" aria-labelledby="dashboard-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Dashboard</p>
            <h2 id="dashboard-title">Overview</h2>
          </div>
          <span className="refresh-meta" aria-live="polite">
            {isDashboardLoading
              ? "Updating..."
              : lastRefreshAt
                ? `Updated ${formatProcessedAt(lastRefreshAt.toISOString())}`
                : "Ready"}
          </span>
        </div>

        {dashboardError ? (
          <div className="notice notice--error" role="alert">
            <strong>Dashboard error</strong>
            <span>{dashboardError}</span>
          </div>
        ) : null}

        <div className="stat-grid">
          <article className="stat-card">
            <span>Total files</span>
            <strong>{formatRecordCount(dashboardStatistics.totalFiles)}</strong>
          </article>
          <article className="stat-card stat-card--success">
            <span>Successful</span>
            <strong>{formatRecordCount(dashboardStatistics.successfulFiles)}</strong>
          </article>
          <article className="stat-card stat-card--failed">
            <span>Failed</span>
            <strong>{formatRecordCount(dashboardStatistics.failedFiles)}</strong>
          </article>
          <article className="stat-card stat-card--records">
            <span>Imported records</span>
            <strong>{formatRecordCount(dashboardStatistics.totalImportedRecords)}</strong>
          </article>
        </div>
      </section>

      <section className="history-workspace" id="history" aria-labelledby="history-title">
        <div className="history-panel">
          <div className="panel-heading panel-heading--split">
            <div>
              <p className="eyebrow">Import history</p>
              <h2 id="history-title">Processed files</h2>
            </div>
            <div className="panel-actions">
              <span className="refresh-meta" aria-live="polite">
                {autoRefreshMessage ||
                  (lastRefreshAt ? `Updated ${formatProcessedAt(lastRefreshAt.toISOString())}` : "")}
              </span>
              <button
                className="secondary-button"
                type="button"
                onClick={() => void refreshDashboardAndHistory()}
                disabled={isHistoryLoading || isDashboardLoading}
              >
                {isHistoryLoading ? "Loading..." : "Refresh"}
              </button>
            </div>
          </div>

          <div className="table-shell">
            <table className="history-table">
              <thead>
                <tr>
                  <th scope="col">File name</th>
                  <th scope="col">Status</th>
                  <th scope="col">Records</th>
                  <th scope="col">Processed</th>
                  <th scope="col">Details</th>
                </tr>
              </thead>
              <tbody>
                {isHistoryLoading ? (
                  <tr>
                    <td className="table-state" colSpan={5}>
                      Loading imports...
                    </td>
                  </tr>
                ) : historyError ? (
                  <tr>
                    <td className="table-state table-state--error" colSpan={5}>
                      {historyError}
                    </td>
                  </tr>
                ) : importHistory.length === 0 ? (
                  <tr>
                    <td className="table-state" colSpan={5}>
                      No import records found.
                    </td>
                  </tr>
                ) : (
                  importHistory.map((importRecord) => (
                    <tr
                      className={selectedImportId === importRecord.id ? "history-row--selected" : ""}
                      key={importRecord.id}
                    >
                      <td>
                        <button
                          className="link-button"
                          type="button"
                          onClick={() => setSelectedImportId(importRecord.id)}
                        >
                          {importRecord.fileName}
                        </button>
                      </td>
                      <td>
                        <span className={getStatusClassName(importRecord.status)}>
                          {importRecord.status}
                        </span>
                      </td>
                      <td>{formatRecordCount(importRecord.recordCount)}</td>
                      <td>{formatProcessedAt(importRecord.processedAt)}</td>
                      <td>
                        <button
                          className="secondary-button table-action"
                          type="button"
                          onClick={() => setSelectedImportId(importRecord.id)}
                        >
                          View
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        <aside className="history-details" aria-label="Import details">
          <div className="panel-heading">
            <p className="eyebrow">Details</p>
            <h2>Import details</h2>
          </div>

          {isDetailLoading ? (
            <div className="details-state">Loading import...</div>
          ) : detailError ? (
            <div className="notice notice--error" role="alert">
              <strong>Details error</strong>
              <span>{detailError}</span>
            </div>
          ) : selectedImport ? (
            <dl className="details-list">
              <div>
                <dt>File name</dt>
                <dd>{selectedImport.fileName}</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>
                  <span className={getStatusClassName(selectedImport.status)}>
                    {selectedImport.status}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Record count</dt>
                <dd>{formatRecordCount(selectedImport.recordCount)}</dd>
              </div>
              <div>
                <dt>Processed date</dt>
                <dd>{formatProcessedAt(selectedImport.processedAt)}</dd>
              </div>
              <div>
                <dt>Error message</dt>
                <dd className="error-message">{selectedImport.errorMessage ?? "None"}</dd>
              </div>
            </dl>
          ) : (
            <div className="details-state">No import selected.</div>
          )}
        </aside>
      </section>

    </main>
  );
}

export default App;
