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
};

type ImportHistoryItem = {
  id: number;
  fileName: string;
  status: string;
  recordCount: number;
  processedAt: string;
  errorMessage?: string | null;
};

const supportedExtensions = [".csv", ".xlsx"];
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5107").replace(
  /\/$/,
  ""
);

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

  if (contentType.includes("application/json")) {
    return (await response.json()) as UploadApiResponse;
  }

  const message = await response.text();

  return {
    error: message || response.statusText
  };
}

async function readApiError(response: Response, fallbackMessage: string) {
  const responseBody = await readUploadResponse(response);

  return responseBody.error ?? responseBody.message ?? fallbackMessage;
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
  return status.toLowerCase() === "success" ? "status-pill status-pill--success" : "status-pill status-pill--failed";
}

function App() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [status, setStatus] = useState<UploadStatus>({ kind: "idle" });
  const [isUploading, setIsUploading] = useState(false);
  const [importHistory, setImportHistory] = useState<ImportHistoryItem[]>([]);
  const [selectedImportId, setSelectedImportId] = useState<number | null>(null);
  const [selectedImport, setSelectedImport] = useState<ImportHistoryItem | null>(null);
  const [isHistoryLoading, setIsHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState("");
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const selectedFileExtension = useMemo(
    () => (selectedFile ? getFileExtension(selectedFile.name).replace(".", "") : ""),
    [selectedFile]
  );

  const canUpload = selectedFile !== null && isSupportedFile(selectedFile) && !isUploading;

  async function loadImportHistory(signal?: AbortSignal) {
    setIsHistoryLoading(true);
    setHistoryError("");

    try {
      const response = await fetch(`${apiBaseUrl}/api/imports`, { signal });

      if (!response.ok) {
        throw new Error(await readApiError(response, "Import history could not be loaded."));
      }

      const imports = (await response.json()) as ImportHistoryItem[];

      setImportHistory(imports);
      setSelectedImportId((currentId) => {
        if (currentId !== null && imports.some((importRecord) => importRecord.id === currentId)) {
          return currentId;
        }

        return imports[0]?.id ?? null;
      });

      if (imports.length === 0) {
        setSelectedImport(null);
      }
    } catch (error) {
      if (isAbortError(error)) {
        return;
      }

      setImportHistory([]);
      setSelectedImportId(null);
      setSelectedImport(null);
      setHistoryError(error instanceof Error ? error.message : "Import history could not be loaded.");
    } finally {
      if (!signal?.aborted) {
        setIsHistoryLoading(false);
      }
    }
  }

  useEffect(() => {
    const abortController = new AbortController();

    void loadImportHistory(abortController.signal);

    return () => abortController.abort();
  }, []);

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
          message: responseBody.error ?? responseBody.message ?? "Upload failed."
        });
        return;
      }

      setStatus({
        kind: "success",
        fileName: responseBody.fileName ?? selectedFile.name,
        message: responseBody.message ?? "File uploaded successfully."
      });
      void loadImportHistory();
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
        <nav aria-label="Main navigation">
          <a href="#dashboard">Dashboard</a>
          <a href="#upload">Upload</a>
          <a aria-current="page" href="#history">
            History
          </a>
        </nav>
      </header>

      <section className="history-workspace" id="history" aria-labelledby="history-title">
        <div className="history-panel">
          <div className="panel-heading panel-heading--split">
            <div>
              <p className="eyebrow">Import history</p>
              <h2 id="history-title">Processed files</h2>
            </div>
            <button
              className="secondary-button"
              type="button"
              onClick={() => void loadImportHistory()}
              disabled={isHistoryLoading}
            >
              {isHistoryLoading ? "Loading..." : "Refresh"}
            </button>
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

      <section className="upload-workspace" id="upload" aria-labelledby="upload-title">
        <div className="upload-panel">
          <div className="panel-heading">
            <p className="eyebrow">File intake</p>
            <h2 id="upload-title">Upload employee data</h2>
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
            <label
              className={`file-target ${selectedFile ? "file-target--selected" : ""}`}
              htmlFor="employee-file"
            >
              <span className="file-target__name">
                {selectedFile ? selectedFile.name : "Select CSV or XLSX file"}
              </span>
              <span className="file-target__meta">
                {selectedFile
                  ? `${formatFileSize(selectedFile.size)} - ${selectedFileExtension.toUpperCase()}`
                  : "CSV and XLSX"}
              </span>
            </label>

            <div className="upload-actions">
              <button className="primary-button" type="submit" disabled={!canUpload}>
                {isUploading ? "Uploading..." : "Upload file"}
              </button>
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

        <aside className="import-summary" aria-label="Upload details">
          <div>
            <span>Accepted formats</span>
            <strong>CSV, XLSX</strong>
          </div>
          <div>
            <span>API target</span>
            <strong>{apiBaseUrl}</strong>
          </div>
          <div>
            <span>Queue status</span>
            <strong>{isUploading ? "Uploading" : "Ready"}</strong>
          </div>
        </aside>
      </section>
    </main>
  );
}

export default App;
