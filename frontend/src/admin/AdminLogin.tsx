import { FormEvent, useState } from "react";
import { verifyAndStoreAdminKey } from "./adminClient";

interface Props {
  onSuccess: () => void;
}

export default function AdminLogin({ onSuccess }: Props) {
  const [key, setKey] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!key.trim()) return;
    setError("");
    setLoading(true);
    const ok = await verifyAndStoreAdminKey(key.trim());
    setLoading(false);
    if (ok) {
      onSuccess();
    } else {
      setError("Invalid admin key.");
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-4">
      <form onSubmit={handleSubmit} className="w-full max-w-sm bg-slate-900 border border-slate-700 rounded-lg p-6">
        <h1 className="text-lg font-semibold mb-4">Control Room</h1>
        <label className="block text-sm text-slate-400 mb-1" htmlFor="admin-key">
          Admin key
        </label>
        <input
          id="admin-key"
          type="password"
          value={key}
          onChange={(e) => setKey(e.target.value)}
          autoFocus
          autoComplete="off"
          className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm mb-3 text-slate-100 focus:outline-none focus:border-slate-400"
        />
        {error && <p className="text-red-400 text-sm mb-3">{error}</p>}
        <button
          type="submit"
          disabled={loading || !key.trim()}
          className="w-full rounded-md bg-slate-100 text-slate-900 font-semibold py-2 text-sm disabled:opacity-50"
        >
          {loading ? "Checking…" : "Enter"}
        </button>
      </form>
    </div>
  );
}
