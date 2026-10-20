# Security

## Reporting a problem

Please do not open a public issue for a security problem. Instead, report it
privately through the repository's security advisory form, or contact the
maintainers directly. We will acknowledge it, work on a fix, and credit you in the
release notes unless you would rather stay anonymous.

## What BytePulse does with your data

- **No telemetry.** The application does not send anything anywhere.
- **No network access.** The application does not use the network at all.
- **Nothing leaves the machine.** Scans are performed locally and results are kept
  in memory; the command line tool writes only to standard output and standard
  error unless you redirect it.

## What BytePulse asks of the system

- **Reading directories and file metadata.** This is what a disk usage tool does.
  It requires no special privilege; directories you cannot read are reported and
  skipped.
- **No elevation.** The application never asks to run as administrator. A privileged
  NTFS mode is described in the roadmap but is not implemented.

## Things to be aware of

- BytePulse opens files and reads their metadata. It does not read file contents
  except when you explicitly ask it to find duplicates, and even then only for
  files whose size suggests they might be duplicates.
- Paths and file names are displayed as the file system reports them. A name
  containing unusual characters is shown literally and is never interpreted as
  markup or as a command.
- The command line tool's output includes file paths. Treat exported reports as you
  would treat the directory listing itself.
