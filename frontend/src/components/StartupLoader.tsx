// Shown until the API answers the app's first requests (the theme and the
// tours). On the hosting's free plan the API sleeps when idle and takes up
// to a minute to wake, which used to leave a blank page. Its colours are
// fixed rather than the theme's, since the theme comes from that same API.
// The note fades in after a few seconds by CSS alone (.startup-note in
// index.css), so a quick answer only ever shows the spinner.
export default function StartupLoader() {
  return (
    <div
      role="status"
      className="min-h-screen bg-[#11161a] text-[#e8ece6] flex flex-col items-center justify-center gap-6 px-4 text-center"
    >
      <h1 className="font-['Archivo_Black',sans-serif] text-3xl sm:text-4xl tracking-wide">
        ID the <span className="text-[#c9e265]">Athlete</span>
      </h1>
      <div
        aria-hidden="true"
        className="h-10 w-10 rounded-full border-4 border-[#e8ece6]/20 border-t-[#c9e265] motion-safe:animate-spin"
      />
      <p className="startup-note text-sm text-[#9aa39a] max-w-xs">
        Warming up the server. The first visit can take up to a minute.
      </p>
    </div>
  );
}
