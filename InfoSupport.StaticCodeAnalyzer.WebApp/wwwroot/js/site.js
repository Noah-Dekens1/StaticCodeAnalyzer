import { getCmInstance } from "./../_content/GaelJ.BlazorCodeMirror6/index.js";

window.scrollToLine = (id, lineNumber) => {
    const cmInstance = getCmInstance(id);
    if (cmInstance) {
        const view = cmInstance.view;
        if (view) {
            const position = view.state.doc.line(lineNumber);
            view.dispatch({
                selection: { head: position.from, anchor: position.to },
                scrollIntoView: true
            })
        }
    }
};