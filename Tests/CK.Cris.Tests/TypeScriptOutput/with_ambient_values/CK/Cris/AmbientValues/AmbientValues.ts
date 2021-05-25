export class AmbientValues implements IAmbientValues, IColoredAmbientValues {
    color: string;
    /**
     * Factory method that exposes all the properties as parameters.
     **/
    static create(color: string): AmbientValues

    /**
     * Creates a new command and calls a configurator for it.
     * @param config A function that configures the new command.
     **/
    static create(config: (c: AmbientValues) => void): AmbientValues
    // Implementation.
    static create(color: string | ((c: AmbientValues) => void)) {
        const c = new AmbientValues();
        if (typeof color === 'function') color(c);
        else {
            c.color = color;
            return c;
        }

    }
}
export interface IAmbientValues {
}
export interface IColoredAmbientValues extends IAmbientValues {
    color: string;
}
