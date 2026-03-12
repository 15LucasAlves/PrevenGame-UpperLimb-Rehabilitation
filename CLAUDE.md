# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**PrevenGame** — *Adaptive Serious Game for Upper-Limb Rehabilitation*
Final year project for the Licenciatura em Engenharia de Desenvolvimento de Jogos Digitais at IPCA (Escola Superior de Tecnologia).

- **Student:** Lucas Ferreira Alves (nº 27922) — a27922@alunos.ipca.pt
- **Supervisors:** Pedro Lobo (pjlobo@ipca.pt) and João L. Vilaça (jvilaca@ipca.pt)
- **Duration:** 400 hours

## Project Goal

Develop a serious game that supports personalised upper-limb rehabilitation by integrating with the **PrevenCare** health system and adapting game difficulty and content in real time based on movement data from sensor hardware. The game transforms physiotherapy exercises into task-oriented challenges that simulate daily-life activities, and includes a clinician/physiotherapist interface for monitoring patient progress.

## Repository Structure

```
Documentos/       # Official academic documents (PDF)
  PropostaProjecto.pdf   # Full project proposal
  ANEXO-II.pdf           # Formal enrolment form
  ANEXO-V.pdf            # Signed project term (approved 29/01/2026)
Projeto/          # Game source code (to be developed)
TextFiles/        # Written deliverables and research notes
  revisao_bibliografica.md  # Bibliographic review (serious games + upper-limb rehab)
```

## Language

All written deliverables, comments, and documentation in this repository are in **Portuguese (Portugal)**.

## Key Technical Context

- The game is intended to interface with **PrevenCare** (external health platform) for patient data
- Movement input comes from **sensor hardware** — the game reads real-time motion data
- Difficulty adaptation must respond to real-time sensor input within a session
- The physiotherapist interface is a first-class feature, not an afterthought
- Game engine and target platform are yet to be decided — place source code under `Projeto/`
