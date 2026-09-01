import { ChangeEvent, FormEvent, useMemo, useRef, useState } from "react";
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

function App() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [status, setStatus] = useState<UploadStatus>({ kind: "idle" });
  const [isUploading, setIsUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const selectedFileExtension = useMemo(
    () => (selectedFile ? getFileExtension(selectedFile.name).replace(".", "") : ""),
    [selectedFile]
  );

  const canUpload = selectedFile !== null && isSupportedFile(selectedFile) && !isUploading;

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
          <a aria-current="page" href="#upload">
            Upload
          </a>
          <a href="#history">History</a>
        </nav>
      </header>

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
              <button type="submit" disabled={!canUpload}>
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
